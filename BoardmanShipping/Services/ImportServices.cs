// ==========================  ImportService.cs  =============================
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;              // ← NEW
using System.Windows;                             // only for MessageBox
using BoardmanShipping.Models;

namespace BoardmanShipping.Services
{
    /// <summary>Imports Sage SOP orders into the Access back-end (pure ODBC).</summary>
    public static class ImportService
    {
        //--------------------------------------------------------------------
        // Connection strings – adjust if you move either file or DSN
        //--------------------------------------------------------------------
        private const string SageConnStr = @"DSN=SageLine50v31;Uid=ODBC;Pwd=BOATRA;";
        private const string AccessConnStr =
            @"Driver={Microsoft Access Driver (*.mdb, *.accdb)};" +
            @"DBQ=C:\ShippingApp\Shipping_be.accdb;" +
            @"ExtendedAnsiSQL=1;";

        static ImportService() =>
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        #region ===== PUBLIC – grid & 'Import Checked' ============================

        public static List<ImportItem> LoadSelection(int top = 20, long? filter = null)
        {
            var list = new List<ImportItem>();
            using var cn = new OdbcConnection(SageConnStr);
            cn.Open();

            string sql = filter is null
                ? @"SELECT ORDER_NUMBER, NAME, ORDER_DATE
                     FROM SALES_ORDER
                    ORDER BY ORDER_NUMBER DESC"
                : $@"SELECT ORDER_NUMBER, NAME, ORDER_DATE
                      FROM SALES_ORDER
                     WHERE ORDER_NUMBER = {filter.Value}";

            using var rd = new OdbcCommand(sql, cn).ExecuteReader();
            int taken = 0;
            while (rd.Read() && (filter is not null || taken < top))
            {
                list.Add(new ImportItem
                {
                    OrderNumber = DbOrNull<int>(rd.GetValue(0)) ?? 0,
                    CustomerName = rd.IsDBNull(1) ? string.Empty : rd.GetString(1),
                    OrderDate = rd.IsDBNull(2) ? null : rd.GetDateTime(2),
                    ToImport = false
                });
                taken++;
            }
            return list;
        }

        public static void ImportSelected(IEnumerable<ImportItem> rows)
        {
            foreach (var r in rows.Where(x => x.ToImport))
                ProcessOrder(r.OrderNumber);
        }

        #endregion

        #region ===== CORE IMPORT =================================================

        private static void ProcessOrder(long soNum)
        {
            using var acc = new OdbcConnection(AccessConnStr);
            acc.Open();

            Header hdr = GetHeader(soNum);

            // -------- detail lines (literal SQL => avoids HYC00)
            string detSql = $@"
SELECT ITEM_NUMBER, QTY_ORDER, STOCK_CODE, DESCRIPTION
FROM   SOP_ITEM
WHERE  ORDER_NUMBER = {soNum}";

            var det = new DataTable();
            using (var da = new OdbcDataAdapter(detSql, SageConnStr))
                da.Fill(det);

            if (det.Rows.Count == 0) return;

            var barcodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (DataRow ln in det.Rows)
            {
                int itemNo = DbOrNull<int>(ln["ITEM_NUMBER"]) ?? 0;
                int qty = DbOrNull<int>(ln["QTY_ORDER"]) ?? 0;
                string partNo = ln["STOCK_CODE"] as string ?? string.Empty;
                string desc = ln["DESCRIPTION"] as string ?? string.Empty;

                if (partNo.Equals("M", StringComparison.OrdinalIgnoreCase)) continue;

                string trackId = $"{soNum}-{itemNo + 1}";

                // ------------ barcode (cache) ---------------------------------
                if (!barcodes.TryGetValue(partNo, out var barcode))
                {
                    barcode = ExecScalar(acc,
                               $"SELECT BARCODE FROM STOCK WHERE STOCK_CODE='{partNo.Replace("'", "''")}'")
                              as string ?? string.Empty;
                    barcodes[partNo] = barcode;
                }

                // ------------ weight + boxes / pallets -------------------------
                decimal weight = EnsureWeightLogged(acc, soNum, hdr.Analysis2);
                (int box, int pal) = EnsureBoxPalletLogged(acc, soNum, hdr.Analysis1);

                // ------------ misc normalisation -------------------------------
                string acctName = string.IsNullOrWhiteSpace(hdr.Notes1)
                                ? hdr.Name ?? string.Empty
                                : $"{hdr.Name} – {hdr.Notes1}";
                string? custOrd = string.IsNullOrWhiteSpace(hdr.CustOrderNo) ? null : hdr.CustOrderNo;

                // ------------ INSERT  /  UPDATE --------------------------------
                if (!RecordExists(acc, "SalesOrders", "TrackId", trackId))
                {
                    ExecNonQuery(acc, BuildInsert(
                        soNum, desc, partNo, qty, acctName, custOrd,
                        weight, box, pal, trackId, barcode, hdr));
                }
                else
                {
                    ExecNonQuery(acc, BuildUpdate(
                        soNum, desc, partNo, qty, acctName, custOrd,
                        weight, box, pal, trackId, barcode, hdr));
                }

                // ------------ tracking log -------------------------------------
                if (!RecordExists(acc, "OrderTracking", "TrackId", trackId))
                {
                    ExecNonQuery(acc,
                        $@"INSERT INTO OrderTracking (TrackId,Sonum,CreatedDate)
                           VALUES ('{trackId}',{soNum},
                                  #{DateTime.Now:yyyy-MM-dd HH:mm:ss}#)");
                }
            }
        }

        #endregion

        #region ===== LITERAL SQL BUILDERS (unchanged) ============================

        private static string BuildInsert(
            long soNum, string desc, string part, int qty, string acctName, string? custOrd,
            decimal weight, int box, int pal, string trackId, string barcode, Header h) => $@"
INSERT INTO SalesOrders
 (Sonum,[Date],Acctref,Acctname,Itemdesc,Partno,Qty,Custorderno,Salesamount,DelDate,
  Analysis1,Analysis2,NotesLine1,Deladd1,Deladd4,Deladd3,Deladd5,Deladd6,
  Box,Pallet,TrackId,Barcode)
VALUES (
 {soNum},{Sql(h.OrderDate)},{Sql(h.AcctRef)},{Sql(acctName)},{Sql(desc)},{Sql(part)},
 {qty},{Sql(custOrd)},{Sql(h.InvoiceNet)},{Sql(h.DelDate)},
 {Sql(h.Analysis1)},{weight.ToString(CultureInfo.InvariantCulture)},{Sql(h.Notes1)},
 {Sql(h.Del1)},{Sql(h.Del2)},{Sql(h.Del3)},{Sql(h.Del4)},{Sql(h.Del5)},
 {box},{pal},{Sql(trackId)},{Sql(barcode)});";

        private static string BuildUpdate(
            long soNum, string desc, string part, int qty, string acctName, string? custOrd,
            decimal weight, int box, int pal, string trackId, string barcode, Header h) => $@"
UPDATE SalesOrders SET
 [Date]      ={Sql(h.OrderDate)},
 Acctref     ={Sql(h.AcctRef)},
 Acctname    ={Sql(acctName)},
 Itemdesc    ={Sql(desc)},
 Partno      ={Sql(part)},
 Qty         ={qty},
 Custorderno ={Sql(custOrd)},
 Salesamount ={Sql(h.InvoiceNet)},
 DelDate     ={Sql(h.DelDate)},
 Analysis1   ={Sql(h.Analysis1)},
 Analysis2   ={weight.ToString(CultureInfo.InvariantCulture)},
 NotesLine1  ={Sql(h.Notes1)},
 Deladd1     ={Sql(h.Del1)},
 Deladd4     ={Sql(h.Del2)},
 Deladd3     ={Sql(h.Del3)},
 Deladd5     ={Sql(h.Del4)},
 Deladd6     ={Sql(h.Del5)},
 Box         ={box},
 Pallet      ={pal},
 Barcode     ={Sql(barcode)}
WHERE TrackId='{trackId}';";

        #endregion

        #region ===== SAFE LITERAL  /  Small helpers =============================

        private static string Sql(object? v) =>
            v switch
            {
                null or DBNull => "NULL",
                DateTime dt => $"#{dt:yyyy-MM-dd HH:mm:ss}#",
                bool b => b ? "1" : "0",
                byte or sbyte or short or ushort
                  or int or uint or long or ulong
                  or float or double or decimal
                                => Convert.ToString(v, CultureInfo.InvariantCulture)!,
                _ => $"'{Convert.ToString(v)!.Replace("'", "''")[..Math.Min(255, Convert.ToString(v)!.Length)]}'"
            };

        private static T? DbOrNull<T>(object v) where T : struct =>
            v == DBNull.Value ? (T?)null : (T)Convert.ChangeType(v, typeof(T), CultureInfo.InvariantCulture);

        #endregion

        #region ===== HEADER + BOX/PALLET + WEIGHT HELPERS =======================

        private sealed record Header
        {
            public DateTime? OrderDate;
            public string? AcctRef;
            public string? Name;
            public string? CustOrderNo;
            public decimal? InvoiceNet;
            public DateTime? DelDate;
            public string Analysis1 = "";
            public string Analysis2 = "";
            public string? Notes1;
            public string? Del1; public string? Del2; public string? Del3;
            public string? Del4; public string? Del5;
        }

        private static Header GetHeader(long soNum)
        {
            string sql = $@"
SELECT  ORDER_DATE, ACCOUNT_REF, NAME,
        CUST_ORDER_NUMBER, INVOICE_NET, DESPATCH_DATE,
        ANALYSIS_1, ANALYSIS_2, NOTES_1,
        DEL_ADDRESS_1, DEL_ADDRESS_2, DEL_ADDRESS_3,
        DEL_ADDRESS_4, DEL_ADDRESS_5
FROM    SALES_ORDER
WHERE   ORDER_NUMBER = {soNum}";

            var t = new DataTable();
            using (var da = new OdbcDataAdapter(sql, SageConnStr))
                da.Fill(t);

            if (t.Rows.Count == 0)
                throw new($"Order {soNum} not found in SALES_ORDER");

            var r = t.Rows[0];
            return new Header
            {
                OrderDate = DbOrNull<DateTime>(r["ORDER_DATE"]),
                AcctRef = r["ACCOUNT_REF"] as string,
                Name = r["NAME"] as string,
                CustOrderNo = r["CUST_ORDER_NUMBER"] as string,
                InvoiceNet = DbOrNull<double>(r["INVOICE_NET"]) is double d ? (decimal?)d : null,
                DelDate = DbOrNull<DateTime>(r["DESPATCH_DATE"]),
                Analysis1 = r["ANALYSIS_1"] as string ?? "",
                Analysis2 = r["ANALYSIS_2"] as string ?? "",
                Notes1 = r["NOTES_1"] as string,
                Del1 = r["DEL_ADDRESS_1"] as string,
                Del2 = r["DEL_ADDRESS_2"] as string,
                Del3 = r["DEL_ADDRESS_3"] as string,
                Del4 = r["DEL_ADDRESS_4"] as string,
                Del5 = r["DEL_ADDRESS_5"] as string
            };
        }

        /* ------------------------------------------------------------------
         * PARSE  “DPD x N”   → boxes = N
         *        “PALLET x N”→ pallets = N
         *        no number   → quantity = 1
         * ----------------------------------------------------------------*/
        private static (int Box, int Pal) ParseCustomField1(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt))
                return (0, 0);

            bool isDPD = txt.IndexOf("DPD", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isPallet = txt.IndexOf("PALLET", StringComparison.OrdinalIgnoreCase) >= 0;

            // first integer in the text; default = 1
            var m = Regex.Match(txt, @"\d+");
            int qty = m.Success ? int.Parse(m.Value, CultureInfo.InvariantCulture) : 1;

            if (isDPD) return (qty, 0);
            if (isPallet) return (0, qty);
            return (0, 0);
        }

        /// <summary>
        /// Returns (box, pallet) for the *first* line of the SONUM.
        /// All following lines for that SONUM get 0/0.</summary>
        private static (int Box, int Pal) EnsureBoxPalletLogged(
            OdbcConnection acc, long soNum, string analysis1)
        {
            bool first = !RecordExists(acc, "TempProcessedCustomField1", "Sonum", soNum);
            if (!first) return (0, 0);

            var (box, pal) = ParseCustomField1(analysis1);

            ExecNonQuery(acc,
                $"INSERT INTO TempProcessedCustomField1 (Sonum) VALUES ({soNum})");
            return (box, pal);
        }

        /// <summary>
        /// Returns the weight (Analysis-2) on the first line only.</summary>
        private static decimal EnsureWeightLogged(
            OdbcConnection acc, long soNum, string analysis2)
        {
            bool first = !RecordExists(acc, "TempProcessedSonum", "Sonum", soNum);
            if (!first) return 0m;

            double? numeric = ExtractNumeric(analysis2);
            ExecNonQuery(acc,
                $"INSERT INTO TempProcessedSonum (Sonum) VALUES ({soNum})");
            return numeric is null ? 0m : (decimal)numeric.Value;
        }

        #endregion

        #region ===== EXECUTE HELPERS + TRACE (unchanged) ========================

        private static void ShowDiag(string sql, IReadOnlyList<object?> args, OdbcException ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== ODBC ERROR =====");
            sb.AppendLine(ex.Message);
            sb.AppendLine($"SQLState  : {ex.Errors[0].SQLState}");
            sb.AppendLine($"NativeCode: {ex.Errors[0].NativeError}");
            sb.AppendLine(); sb.AppendLine("⟨SQL⟩"); sb.AppendLine(sql);
            sb.AppendLine(); sb.AppendLine("⟨parameters⟩");
            for (int i = 0; i < args.Count; i++)
            {
                var v = args[i];
                sb.AppendLine($" @p{i + 1,-2} │ {v?.GetType().Name ?? "null",-12} │ {v ?? "NULL"}");
            }
            MessageBox.Show(sb.ToString(), "ODBC trace", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static void ExecNonQuery(OdbcConnection c, string sql)
        {
            using var cmd = new OdbcCommand(sql, c);
            try { cmd.ExecuteNonQuery(); }
            catch (OdbcException ex) { ShowDiag(sql, Array.Empty<object?>(), ex); throw; }
        }

        private static object? ExecScalar(OdbcConnection c, string sql)
        {
            using var cmd = new OdbcCommand(sql, c);
            try { return cmd.ExecuteScalar(); }
            catch (OdbcException ex) { ShowDiag(sql, Array.Empty<object?>(), ex); throw; }
        }

        /* ---------- RecordExists : builds literal safely (numeric/date/text) */
        private static bool RecordExists(OdbcConnection c, string tbl, string col, object val)
        {
            string lit = val switch
            {
                sbyte or byte or short or ushort
              or int or uint or long or ulong
              or float or double or decimal
                    => Convert.ToString(val, CultureInfo.InvariantCulture)!,
                DateTime dt => $"#{dt:yyyy-MM-dd HH:mm:ss}#",
                _ => $"'{Convert.ToString(val)!.Replace("'", "''")}'"
            };

            string sql = $"SELECT 1 FROM {tbl} WHERE {col} = {lit}";
            using var cmd = new OdbcCommand(sql, c);
            using var rd = cmd.ExecuteReader();
            return rd.Read();
        }

        private static double? ExtractNumeric(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt)) return null;
            string s = new string(txt.Where(c => char.IsDigit(c) || c is '.' or ',').ToArray());
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
                 ? d : null;
        }

        #endregion
    }
}
