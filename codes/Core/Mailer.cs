using System.Net;
using System.Net.Mail;

namespace CarRental.Core
{
    public static class Mailer
    {
        public static string Server => Store.ConfigValue("app.mail.server");

        public static string From => Store.ConfigValue("app.mail.addr");

        public static bool Ready
        {
            get
            {
                string pass = Store.ConfigValue("app.mail.password");
                return Server.Length > 0 && From.Contains('@') && !From.StartsWith("noreply@email.", StringComparison.OrdinalIgnoreCase) && pass.Length > 0 && pass != "your-password";
            }
        }

        public static async Task<string> Send(string to, string subject, string text)
        {
            if (!Ready)
            {
                return "Email is not set up on this site.";
            }
            if (!int.TryParse(Store.ConfigValue("app.mail.port"), out int port))
            {
                port = 587;
            }
            string id = Store.ConfigValue("app.mail.id");
            string pass = Store.ConfigValue("app.mail.password");
            string title = Store.ConfigValue("app.mail.title").Replace('_', ' ');
            try
            {
                using SmtpClient client = new SmtpClient(Server, port) { EnableSsl = port != 25, Timeout = 20000 };
                if (Store.ConfigValue("app.mail.credentials") == "1")
                {
                    client.UseDefaultCredentials = true;
                }
                else
                {
                    client.Credentials = new NetworkCredential(id.Length > 0 ? id : From, pass);
                }
                using MailMessage message = new MailMessage(new MailAddress(From, title.Length > 0 ? title : From), new MailAddress(to)) { Subject = subject, Body = text, IsBodyHtml = false };
                await client.SendMailAsync(message);
                return string.Empty;
            }
            catch (SmtpException)
            {
                return "The mail server did not accept the message.";
            }
            catch (FormatException)
            {
                return "That email address is not valid.";
            }
            catch (InvalidOperationException)
            {
                return "The mail settings are not complete.";
            }
        }
    }
}
