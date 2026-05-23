using System.ComponentModel;
using System.Drawing;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.EFCore;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.Parameters;
using DevExpress.XtraReports.UI;
using XafTVF.Module.BusinessObjects;

namespace XafTVF.Module.Reports
{
    // XtraReport bound at runtime to dbo.get_top_customers via the EF Core DbContext.
    // Parameters TopN + Since are auto-populated by XAF's ReportsV2 module from a matching
    // TopCustomersReportParams instance the user filled in the parameter dialog.
    //
    // Visible = false on every parameter is mandatory — otherwise the report viewer shows its
    // own parameter panel and XAF's ReportParametersObjectBase detail view is bypassed.
    public class TopCustomersReport : XtraReport
    {
        public TopCustomersReport()
        {
            Parameters.Add(new Parameter
            {
                Name = "TopN", Type = typeof(int), Value = 10, Visible = false
            });
            Parameters.Add(new Parameter
            {
                Name = "Since", Type = typeof(DateTime), Value = DateTime.Today.AddMonths(-3), Visible = false
            });

            BuildLayout();
            BeforePrint += OnBeforePrint;
        }

        private void BuildLayout()
        {
            var headerFont = new Font("Segoe UI", 10, FontStyle.Bold);
            var rowFont = new Font("Segoe UI", 10);

            var reportHeader = new ReportHeaderBand { HeightF = 60 };
            reportHeader.Controls.Add(new XRLabel
            {
                Text = "Top Customers by Revenue",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                WidthF = 600, HeightF = 28,
                LocationF = new PointF(0, 0)
            });
            var subtitle = new XRLabel
            {
                Font = new Font("Segoe UI", 10),
                WidthF = 600, HeightF = 20,
                LocationF = new PointF(0, 32),
            };
            subtitle.ExpressionBindings.Add(new ExpressionBinding(
                "BeforePrint", "Text",
                "'Top ' + [Parameters.TopN] + ' since ' + FormatString('{0:yyyy-MM-dd}', [Parameters.Since])"));
            reportHeader.Controls.Add(subtitle);
            Bands.Add(reportHeader);

            var pageHeader = new PageHeaderBand { HeightF = 30 };
            pageHeader.Controls.Add(new XRLabel { Text = "Customer", Font = headerFont, WidthF = 300, HeightF = 25, LocationF = new PointF(0, 0) });
            pageHeader.Controls.Add(new XRLabel { Text = "Revenue", Font = headerFont, WidthF = 120, HeightF = 25, LocationF = new PointF(300, 0), TextAlignment = TextAlignment.MiddleRight });
            pageHeader.Controls.Add(new XRLabel { Text = "Orders", Font = headerFont, WidthF = 100, HeightF = 25, LocationF = new PointF(420, 0), TextAlignment = TextAlignment.MiddleRight });
            pageHeader.Controls.Add(new XRLine { LocationF = new PointF(0, 26), WidthF = 520, HeightF = 1 });
            Bands.Add(pageHeader);

            var detail = new DetailBand { HeightF = 22 };
            var nameLbl = new XRLabel { Font = rowFont, WidthF = 300, HeightF = 22, LocationF = new PointF(0, 0) };
            nameLbl.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[Name]"));
            detail.Controls.Add(nameLbl);
            var revenueLbl = new XRLabel { Font = rowFont, WidthF = 120, HeightF = 22, LocationF = new PointF(300, 0), TextAlignment = TextAlignment.MiddleRight };
            revenueLbl.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "FormatString('{0:C2}', [Revenue])"));
            detail.Controls.Add(revenueLbl);
            var ordersLbl = new XRLabel { Font = rowFont, WidthF = 100, HeightF = 22, LocationF = new PointF(420, 0), TextAlignment = TextAlignment.MiddleRight };
            ordersLbl.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[OrderCount]"));
            detail.Controls.Add(ordersLbl);
            Bands.Add(detail);

            var reportFooter = new ReportFooterBand { HeightF = 40 };
            reportFooter.Controls.Add(new XRLine { LocationF = new PointF(0, 5), WidthF = 520, HeightF = 1 });
            reportFooter.Controls.Add(new XRLabel { Text = "Total revenue:", Font = headerFont, WidthF = 280, HeightF = 22, LocationF = new PointF(20, 12), TextAlignment = TextAlignment.MiddleRight });
            // Total revenue: DataBindings + Summary aggregates Revenue across the entire report.
            // (ExpressionBindings is for runtime expressions; XRSummary specifically needs a
            // direct field binding.)
            var totalLbl = new XRLabel
            {
                Font = headerFont,
                WidthF = 120, HeightF = 22,
                LocationF = new PointF(300, 12),
                TextAlignment = TextAlignment.MiddleRight,
                Summary = new XRSummary { Running = SummaryRunning.Report, Func = SummaryFunc.Sum, FormatString = "{0:C2}" }
            };
            totalLbl.DataBindings.Add(new XRBinding("Text", null, nameof(CustomerSummaryRow.Revenue)));
            reportFooter.Controls.Add(totalLbl);
            Bands.Add(reportFooter);

            var pageFooter = new PageFooterBand { HeightF = 20 };
            pageFooter.Controls.Add(new XRPageInfo
            {
                PageInfo = PageInfo.NumberOfTotal,
                Format = "Page {0} of {1}",
                WidthF = 120, HeightF = 18,
                LocationF = new PointF(400, 0),
                TextAlignment = TextAlignment.MiddleRight
            });
            Bands.Add(pageFooter);
        }

        private void OnBeforePrint(object? sender, CancelEventArgs e)
        {
            // Already loaded — let layout passes finish without re-querying.
            if (DataSource is List<CustomerSummaryRow>) return;

            // XAF ReportsV2 does NOT auto-bind individual TopCustomersReportParams properties to
            // report Parameters by name. It bundles the entire param object as a single hidden
            // parameter named "XafReportParametersObject". Extract our typed param object from it.
            var paramObj = (TopCustomersReportParams)Parameters["XafReportParametersObject"].Value;
            var topN = paramObj.TopN;
            var since = paramObj.Since;

            // Mirror the values into the named TopN/Since report Parameters so the header
            // ExpressionBindings ("[Parameters.TopN]" / "[Parameters.Since]") render correctly.
            Parameters["TopN"].Value = topN;
            Parameters["Since"].Value = since;

            var app = XafTVFModule.CurrentApplication
                ?? throw new InvalidOperationException(
                    "XafTVFModule.CurrentApplication is null — the module's Setup() hasn't run yet.");

            // Short-lived persistent OS borrowed for its DbContext, disposed immediately after.
            using var os = app.CreateObjectSpace(typeof(Customer));
            var ctx = (XafTVFEFCoreDbContext)((EFCoreObjectSpace)os).DbContext;
            DataSource = ctx.GetTopCustomers(topN, since).ToList();
        }
    }
}
