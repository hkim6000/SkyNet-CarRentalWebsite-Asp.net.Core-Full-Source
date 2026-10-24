using System.Globalization;
using CarRental.Models;

namespace CarRental.Core
{
    public static class Seed
    {
        private static DateTime checkedDay = DateTime.MinValue;

        private static string Mark => Path.Combine(Store.DataDir, "demo-day.txt");

        private static DateTime? Move(DateTime? d, int days)
        {
            return d == null ? null : d.Value.AddDays(days);
        }

        public static async Task Keep()
        {
            if (!Store.Demo() || checkedDay == DateTime.Today)
            {
                return;
            }
            using (await Store.Hold())
            {
                DateTime today = DateTime.Today;
                DateTime day = today;
                if (File.Exists(Mark))
                {
                    DateTime.TryParseExact((await File.ReadAllTextAsync(Mark)).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
                }
                int n = day == default ? 0 : (today - day.Date).Days;
                if (n > 0)
                {
                    List<BookingRec> bookings = await Store.Rows<BookingRec>("bookings");
                    foreach (BookingRec b in bookings)
                    {
                        b.Start = b.Start.AddDays(n);
                        b.End = b.End.AddDays(n);
                        b.Created = b.Created.AddDays(n);
                        b.PickedUpAt = Move(b.PickedUpAt, n);
                        b.ReturnedAt = Move(b.ReturnedAt, n);
                        b.CancelledAt = Move(b.CancelledAt, n);
                        b.AskEnd = Move(b.AskEnd, n);
                    }
                    List<InspectionRec> ins = await Store.Rows<InspectionRec>("inspections");
                    foreach (InspectionRec i in ins)
                    {
                        i.When = i.When.AddDays(n);
                    }
                    List<ExtensionRec> ext = await Store.Rows<ExtensionRec>("extensions");
                    foreach (ExtensionRec x in ext)
                    {
                        x.When = x.When.AddDays(n);
                        x.OldEnd = x.OldEnd.AddDays(n);
                        x.NewEnd = x.NewEnd.AddDays(n);
                    }
                    List<TransferRec> tr = await Store.Rows<TransferRec>("transfers");
                    foreach (TransferRec t in tr)
                    {
                        t.Depart = t.Depart.AddDays(n);
                        t.Arrive = t.Arrive.AddDays(n);
                        t.Created = t.Created.AddDays(n);
                    }
                    List<CarRec> cars = await Store.Rows<CarRec>("cars");
                    foreach (CarRec c in cars)
                    {
                        c.StatusSince = c.StatusSince.AddDays(n);
                        c.BlockUntil = Move(c.BlockUntil, n);
                    }
                    List<InquiryRec> msgs = await Store.Rows<InquiryRec>("messages");
                    foreach (InquiryRec m in msgs)
                    {
                        m.When = m.When.AddDays(n);
                    }
                    List<ServiceRec> sv = await Store.Rows<ServiceRec>("services");
                    foreach (ServiceRec v in sv)
                    {
                        v.Start = v.Start.AddDays(n);
                        v.End = v.End.AddDays(n);
                        v.Created = v.Created.AddDays(n);
                        v.DoneAt = Move(v.DoneAt, n);
                    }
                    List<RepairRec> rp = await Store.Rows<RepairRec>("repairs");
                    foreach (RepairRec r in rp)
                    {
                        r.Opened = r.Opened.AddDays(n);
                        r.ReadyBy = Move(r.ReadyBy, n);
                        r.ClosedAt = Move(r.ClosedAt, n);
                    }
                    List<IncidentRec> ic = await Store.Rows<IncidentRec>("incidents");
                    foreach (IncidentRec x in ic)
                    {
                        x.When = x.When.AddDays(n);
                        x.RecoveredAt = Move(x.RecoveredAt, n);
                        x.ClosedAt = Move(x.ClosedAt, n);
                    }
                    List<SaleRec> sl = await Store.Rows<SaleRec>("sales");
                    foreach (SaleRec x in sl)
                    {
                        x.Date = x.Date.AddDays(n);
                        x.Created = x.Created.AddDays(n);
                    }
                    List<PaymentRec> pay = await Store.Rows<PaymentRec>("payments");
                    foreach (PaymentRec x in pay)
                    {
                        x.When = x.When.AddDays(n);
                    }
                    await Store.SaveRows("payments", pay);
                    await Store.SaveRows("services", sv);
                    await Store.SaveRows("repairs", rp);
                    await Store.SaveRows("incidents", ic);
                    await Store.SaveRows("sales", sl);
                    await Store.SaveRows("bookings", bookings);
                    await Store.SaveRows("inspections", ins);
                    await Store.SaveRows("extensions", ext);
                    await Store.SaveRows("transfers", tr);
                    await Store.SaveRows("cars", cars);
                    await Store.SaveRows("messages", msgs);
                }
                if (n != 0 || !File.Exists(Mark))
                {
                    Directory.CreateDirectory(Store.DataDir);
                    await File.WriteAllTextAsync(Mark, today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }
            }
            checkedDay = DateTime.Today;
        }
    }
}
