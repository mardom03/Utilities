using Cronos;
using HtmlAgilityPack;
using Serilog;
using Serilog.Core;
using System.Net;
using System.Net.Http;
using System.Net.Mail;

namespace VixEmailer
{
    public class Worker : BackgroundService
    {
        private readonly Logger _log;
        private readonly string _daylight = "0,30 14-21 * * 1-5";
        private readonly string _standard = "0,30 15-22 * * 1-5";
        private readonly HttpClient _client;
        private readonly SmtpClient _smtp;

        public Worker(Logger log, IConfigurationRoot _config)
        {
            _log = log;
            _client = new HttpClient();
            _smtp = new SmtpClient("mail.smtp2go.com")
            {
                Port = 2525,
                Credentials = new NetworkCredential(_config.GetSection("Username").Value, _config.GetSection("Password").Value),
                EnableSsl = true,
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {

                try
                {
                    HttpResponseMessage resp = await _client.GetAsync("https://finance.yahoo.com/quote/%5EVIX?p=^VIX&.tsrc=fin-srch");
                    resp.EnsureSuccessStatusCode();

                    string htmlContent = await resp.Content.ReadAsStringAsync();
                    var htmlDocument = new HtmlDocument();
                    htmlDocument.LoadHtml(htmlContent);

                    string xpathExpression = "//fin-streamer[@data-test=\"qsp-price\"]";
                    HtmlNode selectedElement = htmlDocument.DocumentNode.SelectSingleNode(xpathExpression);

                    if (selectedElement != null && Decimal.TryParse(selectedElement.InnerText, out decimal result))
                    {
                        if(result > 20)
                        {
                            string body = "<h3>The price of VIX is currently " + selectedElement.InnerText + "</h3>";
                            _smtp.Send(new MailMessage("6nitram21@gmail.com", "domaradzki@wisc.edu", "Vix Price Alert", body) { IsBodyHtml = true });
                            _log.Information($"Email sent: VIX is {result}");
                        }
                    }
                    else
                    {
                        throw new Exception("They changed the website");
                    }
                }
                catch (Exception ex)
                {
                    string body = "<h3>Emailer broke for some reason:</h3> <br>" + ex;
                    _smtp.Send(new MailMessage("6nitram21@gmail.com", "domaradzki@wisc.edu", "Vix Price Alert - ERROR", body) { IsBodyHtml = true });
                    _log.Error($"Error: {ex}");
                    await Task.Delay(86400000);
                }

                if (TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now)) await WaitForNextSchedule(_daylight);
                else await WaitForNextSchedule(_standard);
            }
        }

        private async Task WaitForNextSchedule(string cronExpression)
        {
            var parsedExp = CronExpression.Parse(cronExpression);
            var currentUtcTime = DateTimeOffset.UtcNow.UtcDateTime;
            var occurenceTime = parsedExp.GetNextOccurrence(currentUtcTime);

            var delay = occurenceTime.GetValueOrDefault() - currentUtcTime;
            _log.Information($"The run is delayed for {delay}");

            await Task.Delay(delay);
        }
    }
}