using System;
using System.Collections.ObjectModel;
using System.Data.Odbc;
using BoardmanShipping;            // for SalesOrder

namespace BoardmanShipping.Services
{
    public static class OdbcDataService
    {
        private const string ConnectionString =
            @"Driver={Microsoft Access Driver (*.mdb, *.accdb)};Dbq=C:\ShippingApp\Shipping_be.accdb;";

        /// <summary>
        /// Retrieves all SalesOrder rows whose DelDate falls on the given date.
        /// </summary>
        public static ObservableCollection<SalesOrder> GetOrders(DateTime date)
        {
            var list = new ObservableCollection<SalesOrder>();

            using (var conn = new OdbcConnection(ConnectionString))
            {
                conn.Open();

                DateTime start = date.Date;
                DateTime end = start.AddDays(1);
                string s0 = start.ToString("MM\\/dd\\/yyyy");
                string s1 = end.ToString("MM\\/dd\\/yyyy");

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $@"
SELECT
  Acctname,
  Sonum,
  Partno,
  Analysis1,
  Itemdesc,
  OnHold,
  TrackStatus,
  Custorderno,
  Completed,
  Qty,
  DelDate,
  Val(Analysis2) AS ItemWeight,
  NotesLine1,
  Pallet,
  Box
FROM SalesOrders
WHERE DelDate >= #{s0}# AND DelDate < #{s1}#;";

                    using (var rdr = cmd.ExecuteReader())
                    {
                        int iA = rdr.GetOrdinal("Acctname");
                        int i0 = rdr.GetOrdinal("Sonum");
                        int i1 = rdr.GetOrdinal("Partno");
                        int i2 = rdr.GetOrdinal("Analysis1");
                        int i3 = rdr.GetOrdinal("Itemdesc");
                        int i4 = rdr.GetOrdinal("OnHold");
                        int i5 = rdr.GetOrdinal("TrackStatus");
                        int i6 = rdr.GetOrdinal("Custorderno");
                        int i7 = rdr.GetOrdinal("Completed");
                        int i8 = rdr.GetOrdinal("Qty");
                        int i9 = rdr.GetOrdinal("DelDate");
                        int iA2 = rdr.GetOrdinal("ItemWeight");
                        int iN = rdr.GetOrdinal("NotesLine1");
                        int iP = rdr.GetOrdinal("Pallet");
                        int iB = rdr.GetOrdinal("Box");

                        while (rdr.Read())
                        {
                            var so = new SalesOrder
                            {
                                Acctname = rdr.IsDBNull(iA) ? "" : rdr.GetString(iA),
                                Sonum = rdr.IsDBNull(i0) ? 0 : rdr.GetInt32(i0),
                                Partno = rdr.IsDBNull(i1) ? "" : rdr.GetString(i1),
                                Analysis1 = rdr.IsDBNull(i2) ? "" : rdr.GetString(i2),
                                Itemdesc = rdr.IsDBNull(i3) ? "" : rdr.GetString(i3),
                                OnHold = rdr.IsDBNull(i4) ? false : rdr.GetBoolean(i4),
                                TrackStatus = rdr.IsDBNull(i5) ? "" : rdr.GetString(i5),
                                Custorderno = rdr.IsDBNull(i6) ? "" : rdr.GetString(i6),
                                Completed = rdr.IsDBNull(i7) ? false : rdr.GetBoolean(i7),
                                Qty = rdr.IsDBNull(i8) ? 0 : rdr.GetInt32(i8),
                                DelDate = rdr.IsDBNull(i9) ? DateTime.MinValue : rdr.GetDateTime(i9),
                                ItemWeight = rdr.IsDBNull(iA2) ? 0.0 : rdr.GetDouble(iA2),
                                NotesLine1 = rdr.IsDBNull(iN) ? "" : rdr.GetString(iN),
                                Pallet = rdr.IsDBNull(iP) ? 0 : rdr.GetInt32(iP),
                                Box = rdr.IsDBNull(iB) ? 0 : rdr.GetInt32(iB),
                            };
                            list.Add(so);
                        }
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Finds the DelDate for the first order matching the given SO or Custorderno.
        /// Returns null if no match.
        /// </summary>
        public static DateTime? FindOrderDate(string orderKey)
        {
            using (var conn = new OdbcConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT TOP 1 DelDate
FROM SalesOrders
WHERE Sonum = ? OR Custorderno = ?
ORDER BY DelDate;";

                    if (int.TryParse(orderKey, out var soNum))
                        cmd.Parameters.Add("?", OdbcType.Int).Value = soNum;
                    else
                        cmd.Parameters.Add("?", OdbcType.Int).Value = 0;

                    cmd.Parameters.Add("?", OdbcType.VarChar, 50).Value = orderKey;

                    var result = cmd.ExecuteScalar();
                    if (result is DateTime dt)
                        return dt.Date;
                }
            }

            return null;
        }

        /// <summary>
        /// Updates the Completed flag for a single SalesOrder in the DB.
        /// </summary>
        public static void UpdateCompleted(SalesOrder order)
        {
            using (var conn = new OdbcConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
UPDATE SalesOrders
SET Completed = ?
WHERE Sonum = ?
  AND Custorderno = ?
  AND DelDate = ?;";

                    cmd.Parameters.Add("?", OdbcType.Bit).Value = order.Completed ? 1 : 0;
                    cmd.Parameters.Add("?", OdbcType.Int).Value = order.Sonum;
                    cmd.Parameters.Add("?", OdbcType.VarChar, 50).Value = order.Custorderno;
                    cmd.Parameters.Add("?", OdbcType.DateTime).Value = order.DelDate;

                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
