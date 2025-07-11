using BoardmanShipping.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;   // for COMException
using System.Windows;
using System.Windows.Controls;          // for PrintDialog
using System.Windows.Documents;
using System.Windows.Media;

namespace BoardmanShipping.Services
{
    public enum WatermarkType
    {
        None,
        Draft,
        Final
    }

    public static class PrintService
    {
        public static void PrintDailyDiary(
            IEnumerable<SalesOrder> orders,
            DateTime date,
            WatermarkType watermark = WatermarkType.None)
        {
            // --- Define brushes ---
            var headerBrush = new SolidColorBrush(Color.FromRgb(255, 204, 102)); headerBrush.Freeze();
            var purpleBrush = new SolidColorBrush(Color.FromRgb(191, 95, 255)); purpleBrush.Freeze();
            var greenBrush = new SolidColorBrush(Color.FromRgb(0, 153, 0)); greenBrush.Freeze();
            var pinkBrush = new SolidColorBrush(Color.FromArgb(64, 255, 192, 203)); pinkBrush.Freeze();
            var blueBrush = new SolidColorBrush(Color.FromArgb(64, 173, 216, 230)); blueBrush.Freeze();
            var onHoldBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0)); onHoldBrush.Freeze();
            var completedBrush = new SolidColorBrush(Color.FromRgb(173, 216, 230)); completedBrush.Freeze();
            var bothBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)); bothBrush.Freeze();
            var analysisBrush = new SolidColorBrush(Color.FromRgb(0, 100, 0)); analysisBrush.Freeze();

            // --- Create FlowDocument ---
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(30),
                ColumnWidth = double.PositiveInfinity,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11
            };

            // --- Watermark via Background brush if requested ---
            if (watermark != WatermarkType.None)
            {
                string wmText = watermark == WatermarkType.Draft ? "DRAFT" : "FINAL";
                var wmBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)); wmBrush.Freeze();

                var vis = new DrawingVisual();
                using (var ctx = vis.RenderOpen())
                {
                    var ft = new FormattedText(
                        wmText,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        100,
                        wmBrush,
                        1.0);

                    ctx.PushTransform(new TranslateTransform(100, 300));
                    ctx.PushTransform(new RotateTransform(-45));
                    ctx.DrawText(ft, new Point(0, 0));
                    ctx.Pop();
                    ctx.Pop();
                }

                var vb = new VisualBrush(vis)
                {
                    Stretch = Stretch.None,
                    TileMode = TileMode.None,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    Opacity = 0.3
                };
                doc.Background = vb;
            }

            // --- Two-line header ---
            doc.Blocks.Add(new Paragraph(new Run("Daily Shipping Schedule"))
            {
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            });
            doc.Blocks.Add(new Paragraph(new Run(date.ToString("dddd dd MMMM yyyy")))
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            });

            // --- Track totals ---
            var uniqueOrders = new HashSet<int>();
            int overallQty = 0, overallPal = 0, overallBox = 0;

            // --- Per-account groups A→Z ---
            foreach (var group in orders
                .GroupBy(o => o.Acctname)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                // Account header
                doc.Blocks.Add(new Paragraph(new Run(group.Key.ToUpperInvariant() + " –"))
                {
                    Background = headerBrush,
                    FontWeight = FontWeights.Bold,
                    Padding = new Thickness(5),
                    Margin = new Thickness(0, 0, 0, 8)
                });

                // Table setup
                var table = new Table { CellSpacing = 0 };
                foreach (var width in new[] { 70.0, 110.0, 200.0, 50.0, 60.0, 50.0, 50.0, 100.0, 150.0 })
                    table.Columns.Add(new TableColumn { Width = new GridLength(width) });

                // Header row
                var hdrGroup = new TableRowGroup();
                var hdrRow = new TableRow();
                foreach (var header in new[] { "SO", "PartNo", "Description", "Qty", "Weight", "Pallet", "Boxes", "Status", "Analysis" })
                    hdrRow.Cells.Add(CreateCell(header, center: true, isHeader: true));
                hdrGroup.Rows.Add(hdrRow);
                table.RowGroups.Add(hdrGroup);

                // Body rows
                var bodyGroup = new TableRowGroup();
                int? lastSo = null;
                bool usePink = false;
                foreach (var so in group.OrderBy(o => o.Sonum))
                {
                    uniqueOrders.Add(so.Sonum);
                    overallQty += so.Qty;
                    overallPal += so.Pallet;
                    overallBox += so.Box;

                    if (lastSo != so.Sonum)
                    {
                        lastSo = so.Sonum;
                        usePink = !usePink;
                    }
                    var bg = usePink ? pinkBrush : blueBrush;
                    var row = new TableRow();

                    row.Cells.Add(CreateCell(so.Sonum.ToString(), center: true, background: bg));
                    row.Cells.Add(CreateCell(so.Partno, background: bg));
                    row.Cells.Add(CreateCell(so.Itemdesc, background: bg));
                    row.Cells.Add(CreateCell(so.Qty.ToString(), center: true, background: bg));
                    row.Cells.Add(CreateCell(so.ItemWeight.ToString("0"), center: true, background: bg));
                    row.Cells.Add(CreateCell(so.Pallet.ToString(), center: true, background: bg));
                    row.Cells.Add(CreateCell(so.Box.ToString(), center: true, background: bg));

                    // Status
                    string statusText = "";
                    Brush statusBg = bg;
                    if (so.OnHold && so.Completed)
                    {
                        statusText = "On Hold / Completed"; statusBg = bothBrush;
                    }
                    else if (so.OnHold)
                    {
                        statusText = "On Hold"; statusBg = onHoldBrush;
                    }
                    else if (so.Completed)
                    {
                        statusText = "Completed"; statusBg = completedBrush;
                    }
                    row.Cells.Add(CreateCell(statusText, center: true, background: statusBg, bold: !string.IsNullOrEmpty(statusText)));

                    // Analysis
                    var analysis = (so.Analysis1 ?? "").Trim();
                    bool showAnalysis = !string.IsNullOrEmpty(analysis)
                                      && !analysis.StartsWith("DPD", StringComparison.OrdinalIgnoreCase)
                                      && !analysis.StartsWith("Pallet", StringComparison.OrdinalIgnoreCase);
                    row.Cells.Add(CreateCell(showAnalysis ? analysis : "", background: showAnalysis ? analysisBrush : null));

                    bodyGroup.Rows.Add(row);
                }
                table.RowGroups.Add(bodyGroup);

                // Footer group
                var footGroup = new TableRowGroup();
                var footRow = new TableRow();
                footRow.Cells.Add(new TableCell(new Paragraph(new Run("Total"))
                {
                    FontSize = 12,
                    FontWeight = FontWeights.Bold
                })
                {
                    ColumnSpan = 3,
                    Padding = new Thickness(2),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(0, 1, 0, 0)
                });
                int sumQty = group.Sum(x => x.Qty);
                double sumWt = group.Sum(x => x.ItemWeight);
                int sumPal = group.Sum(x => x.Pallet);
                int sumBox = group.Sum(x => x.Box);
                var totalBg = sumPal > 0 ? purpleBrush : greenBrush;

                footRow.Cells.Add(CreateFooterCell(sumQty.ToString()));
                footRow.Cells.Add(CreateFooterCell(sumWt.ToString("0")));
                footRow.Cells.Add(CreateFooterCell(sumPal.ToString(), background: totalBg));
                footRow.Cells.Add(CreateFooterCell(sumBox.ToString(), background: totalBg));
                footGroup.Rows.Add(footRow);
                table.RowGroups.Add(footGroup);

                doc.Blocks.Add(table);
            }

            // Overall summary
            doc.Blocks.Add(new Paragraph { Margin = new Thickness(0, 20, 0, 5) });
            var summary = new Paragraph
            {
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Left
            };
            summary.Inlines.Add(new Run($"Orders: {uniqueOrders.Count}    ")); summary.Inlines.Add(new Run($"Total Qty: {overallQty}    ")); summary.Inlines.Add(new Run($"Total Pallets: {overallPal}    ")); summary.Inlines.Add(new Run($"Total Boxes: {overallBox}"));
            doc.Blocks.Add(summary);

            // Print
            var pd = new PrintDialog();
            if (pd.ShowDialog() == true)
            {
                doc.PageHeight = pd.PrintableAreaHeight;
                doc.PageWidth = pd.PrintableAreaWidth;
                doc.ColumnWidth = pd.PrintableAreaWidth;
                try
                {
                    pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"Daily Shipping Schedule {date:dd MMM yyyy}");
                }
                catch (COMException comEx) when ((uint)comEx.ErrorCode == 0x80070020u)
                {
                    MessageBox.Show("The PDF file is locked or in use. Please close it and try again.", "File Locked", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private static TableCell CreateCell(string text, bool center = false, bool isHeader = false, bool bold = false, Brush? background = null)
        {
            var para = new Paragraph(new Run(text))
            {
                TextAlignment = center ? TextAlignment.Center : TextAlignment.Left,
                Padding = new Thickness(2),
                FontWeight = bold ? FontWeights.Bold : (isHeader ? FontWeights.SemiBold : FontWeights.Normal)
            };
            return new TableCell(para)
            {
                Background = background ?? Brushes.Transparent,
                BorderBrush = isHeader ? Brushes.Gray : null,
                BorderThickness = isHeader ? new Thickness(0, 0, 0, 1) : new Thickness(0)
            };
        }

        private static TableCell CreateFooterCell(string text, Brush? background = null)
        {
            var para = new Paragraph(new Run(text))
            {
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(2)
            };
            return new TableCell(para)
            {
                Background = background ?? Brushes.Transparent,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
        }
    }
}
