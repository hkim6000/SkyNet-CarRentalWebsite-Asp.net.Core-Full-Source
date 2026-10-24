namespace CarRental.Models
{
    public class SiteData
    {
        public string Brand { get; set; } = "Brightroad";
        public string Legal { get; set; } = "Brightroad Car Rental";
        public string Tagline { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NotifyEmail { get; set; } = string.Empty;
        public int CancelHours { get; set; } = 24;
        public int BufferHours { get; set; } = 2;
        public int MinAge { get; set; } = 21;
        public int GraceMinutes { get; set; } = 59;
        public decimal FuelPerEighth { get; set; } = 9.5m;
        public int DailyMiles { get; set; } = 200;
        public decimal MileRate { get; set; } = 0.35m;
        public decimal CleaningFee { get; set; } = 75m;
        public decimal TaxRate { get; set; } = 8m;
        public int MaxVideoMb { get; set; } = 30;
        public int ServiceEvery { get; set; } = 10000;
        public Dictionary<string, PageDoc> Pages { get; set; } = new Dictionary<string, PageDoc>();
    }

    public class BranchRec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Hours { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public bool Open { get; set; } = true;
    }

    public class ClassRec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Example { get; set; } = string.Empty;
        public int Seats { get; set; } = 5;
        public int Doors { get; set; } = 4;
        public int Bags { get; set; } = 2;
        public string Transmission { get; set; } = "Automatic";
        public string Fuel { get; set; } = "Gas";
        public decimal Daily { get; set; }
        public decimal Weekend { get; set; }
        public decimal Weekly { get; set; }
        public decimal Deposit { get; set; }
        public List<string> Features { get; set; } = new List<string>();
        public string Media { get; set; } = string.Empty;
        public bool Show { get; set; } = true;
    }

    public class CarRec
    {
        public string Id { get; set; } = string.Empty;
        public string Plate { get; set; } = string.Empty;
        public string Vin { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Color { get; set; } = string.Empty;
        public string ClassId { get; set; } = string.Empty;
        public string BranchId { get; set; } = string.Empty;
        public int Mileage { get; set; }
        public int Fuel { get; set; } = 8;
        public string Status { get; set; } = "Available";
        public string StatusNote { get; set; } = string.Empty;
        public DateTime? BlockUntil { get; set; }
        public DateTime StatusSince { get; set; }
        public DateTime Purchased { get; set; }
        public decimal Cost { get; set; }
        public DateTime RegistrationDue { get; set; }
        public int ServiceMiles { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class ExtraRec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string Kind { get; set; } = "item";
        public decimal PerDay { get; set; }
        public int MaxQty { get; set; } = 1;
        public bool Show { get; set; } = true;
        public Dictionary<string, int> Stock { get; set; } = new Dictionary<string, int>();
    }

    public class BookedExtra
    {
        public string Id { get; set; } = string.Empty;
        public int Qty { get; set; } = 1;
    }

    public class ChargeRec
    {
        public string Kind { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class BookingRec
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string BranchId { get; set; } = string.Empty;
        public string ClassId { get; set; } = string.Empty;
        public string CarId { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Status { get; set; } = "Reserved";
        public string First { get; set; } = string.Empty;
        public string Last { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string License { get; set; } = string.Empty;
        public string LicenseState { get; set; } = string.Empty;
        public DateTime Birth { get; set; }
        public List<BookedExtra> Extras { get; set; } = new List<BookedExtra>();
        public decimal RateTotal { get; set; }
        public decimal ExtrasTotal { get; set; }
        public List<ChargeRec> Charges { get; set; } = new List<ChargeRec>();
        public decimal Paid { get; set; }
        public string PaidBy { get; set; } = string.Empty;
        public string Source { get; set; } = "web";
        public string Note { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? PickedUpAt { get; set; }
        public DateTime? ReturnedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? AskEnd { get; set; }
        public string AskNote { get; set; } = string.Empty;
        public int Extensions { get; set; }
        public string PayNote { get; set; } = string.Empty;
        public string PayerKind { get; set; } = "self";
        public string InsurerId { get; set; } = string.Empty;
        public string ClaimNo { get; set; } = string.Empty;
        public bool CoverExtras { get; set; }
    }

    public class InsurerRec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int TermsDays { get; set; } = 30;
        public decimal DailyCap { get; set; }
        public bool CoverExtras { get; set; }
        public string Note { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
    }

    public class PaymentRec
    {
        public string Id { get; set; } = string.Empty;
        public string BookingId { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public string Payer { get; set; } = "Customer";
        public string InsurerId { get; set; } = string.Empty;
        public string Method { get; set; } = "Card";
        public decimal Amount { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string By { get; set; } = string.Empty;
    }

    public class InspectionRec
    {
        public string Id { get; set; } = string.Empty;
        public string BookingId { get; set; } = string.Empty;
        public string CarId { get; set; } = string.Empty;
        public string Kind { get; set; } = "out";
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
        public int Mileage { get; set; }
        public int Fuel { get; set; }
        public Dictionary<string, string> Panels { get; set; } = new Dictionary<string, string>();
        public string Notes { get; set; } = string.Empty;
        public List<string> Photos { get; set; } = new List<string>();
    }

    public class ExtensionRec
    {
        public string Id { get; set; } = string.Empty;
        public string BookingId { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
        public DateTime OldEnd { get; set; }
        public DateTime NewEnd { get; set; }
        public string OldCarId { get; set; } = string.Empty;
        public string NewCarId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class TransferRec
    {
        public string Id { get; set; } = string.Empty;
        public string CarId { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public DateTime Depart { get; set; }
        public DateTime Arrive { get; set; }
        public string Status { get; set; } = "Planned";
        public string Note { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class ServiceRec
    {
        public string Id { get; set; } = string.Empty;
        public string CarId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Status { get; set; } = "Planned";
        public string Shop { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public int Mileage { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public DateTime? DoneAt { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class RepairRec
    {
        public string Id { get; set; } = string.Empty;
        public string CarId { get; set; } = string.Empty;
        public string BookingId { get; set; } = string.Empty;
        public string IncidentId { get; set; } = string.Empty;
        public DateTime Opened { get; set; }
        public string Problem { get; set; } = string.Empty;
        public string Shop { get; set; } = string.Empty;
        public decimal Estimate { get; set; }
        public decimal Cost { get; set; }
        public decimal Charged { get; set; }
        public DateTime? ReadyBy { get; set; }
        public string Status { get; set; } = "Open";
        public string Notes { get; set; } = string.Empty;
        public DateTime? ClosedAt { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class IncidentRec
    {
        public string Id { get; set; } = string.Empty;
        public string CarId { get; set; } = string.Empty;
        public string BookingId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public string Place { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PoliceReport { get; set; } = string.Empty;
        public string PoliceAgency { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public DateTime? RecoveredAt { get; set; }
        public List<string> Photos { get; set; } = new List<string>();
        public string Notes { get; set; } = string.Empty;
        public DateTime? ClosedAt { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class SaleRec
    {
        public string Id { get; set; } = string.Empty;
        public string CarId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Buyer { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Mileage { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string PrevStatus { get; set; } = "Available";
        public DateTime Created { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class InquiryRec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public string Status { get; set; } = "New";
    }

    public class MediaItem
    {
        public string Id { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Thumb { get; set; } = string.Empty;
        public string Kind { get; set; } = "photo";
        public string Name { get; set; } = string.Empty;
        public string Alt { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public long Bytes { get; set; }
        public DateTime Added { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class BlockItem
    {
        public string Icon { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class Block
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public bool Hidden { get; set; }
        public string Kicker { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label2 { get; set; } = string.Empty;
        public string Link2 { get; set; } = string.Empty;
        public bool Flip { get; set; }
        public List<BlockItem> Items { get; set; } = new List<BlockItem>();
    }

    public class PageVersion
    {
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
        public List<Block> Blocks { get; set; } = new List<Block>();
    }

    public class PageDoc
    {
        public List<Block> Blocks { get; set; } = new List<Block>();
        public List<PageVersion> Versions { get; set; } = new List<PageVersion>();
        public DateTime Saved { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class AdminUser
    {
        public string Id { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public bool MustChange { get; set; }
        public bool Active { get; set; } = true;
        public bool Manager { get; set; }
        public string SetId { get; set; } = string.Empty;
        public Dictionary<string, string> Perms { get; set; } = new Dictionary<string, string>();
        public bool AllBranches { get; set; } = true;
        public List<string> Branches { get; set; } = new List<string>();
    }

    public class PermSet
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public Dictionary<string, string> Perms { get; set; } = new Dictionary<string, string>();
    }

    public class SessionRec
    {
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime Expires { get; set; }
    }

    public class ActivityRec
    {
        public DateTime When { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool Ok { get; set; } = true;
    }

    public class PublicData
    {
        public SiteData Site { get; set; } = new SiteData();
        public List<MediaItem> Media { get; set; } = new List<MediaItem>();
        public List<BranchRec> Branches { get; set; } = new List<BranchRec>();
        public List<ClassRec> Classes { get; set; } = new List<ClassRec>();
        public List<ExtraRec> Extras { get; set; } = new List<ExtraRec>();
    }
}
