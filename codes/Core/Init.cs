namespace CarRental.Core
{
    public static class Init
    {
        private static readonly string[] Records = { "messages", "activity", "staff", "bookings", "inspections", "extensions", "transfers", "cars", "services", "repairs", "incidents", "sales", "payments", "insurers" };

        public static string Marker => Path.Combine(Store.DataDir, "initialized.txt");

        public static bool Done => File.Exists(Marker);

        public static async Task OnStart()
        {
            if (Store.Demo() || Done)
            {
                return;
            }
            await Run("first start in Production", true);
        }

        public static async Task<int> Run(string why, bool mark, bool keepStaff = false)
        {
            int removed = 0;
            using (await Store.Hold())
            {
                foreach (string name in Records.Where(r => !keepStaff || r != "staff"))
                {
                    string file = Path.Combine(Store.DataDir, name + ".json");
                    if (!File.Exists(file))
                    {
                        continue;
                    }
                    removed += CountItems(file);
                    File.Delete(file);
                }
                if (File.Exists(Store.SessionFile) && !keepStaff)
                {
                    File.Delete(Store.SessionFile);
                }
                if (File.Exists(Store.InitialFile))
                {
                    File.Delete(Store.InitialFile);
                }
                Directory.CreateDirectory(Store.DataDir);
                if (mark)
                {
                    await File.WriteAllTextAsync(Marker, DateTime.UtcNow.ToString("u") + " " + why + " " + removed + " records" + Environment.NewLine);
                }
            }
            await Store.Log("system", "initialize data", why + " · " + removed + " records removed", true);
            return removed;
        }

        private static int CountItems(string file)
        {
            try
            {
                using FileStream s = File.OpenRead(file);
                using System.Text.Json.JsonDocument d = System.Text.Json.JsonDocument.Parse(s);
                return d.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array ? d.RootElement.GetArrayLength() : 1;
            }
            catch (System.Text.Json.JsonException)
            {
                return 0;
            }
        }
    }
}
