using FFXIV_GameSense.Properties;
using System;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Windows.Documents;
using System.Windows.Navigation;
using System.Diagnostics;
using Splat;

namespace FFXIV_GameSense.UI
{
    /// <summary>
    /// Interaction logic for LogInForm.xaml
    /// </summary>
    public partial class LogInForm : Window
    {
        private const string Login2FaUrl = FFXIVHunts.baseUrl+"Account/LoginWith2fa";
        private const string RemoteLoginUrl = FFXIVHunts.baseUrl + "Account/RemoteLogin";
        private const string AccountLoginUrl = FFXIVHunts.baseUrl + "Account/Login";
        private const string IdentityCookieName = ".AspNetCore.Identity.Application";
        private const string TwoFactorUserIdCookieName = "Identity.TwoFactorUserId";
        private const string TwoFactorRememberMeCookieName = "Identity.TwoFactorRememberMe";
        internal const string XIVHuntNet = "XIVHunt.net";
        internal CookieContainer receivedCookies = new CookieContainer(2);

        public LogInForm(ushort wid)
        {
            InitializeComponent();
            string text;
            if (XIVDB.GameResources.IsChineseWorld(wid))
                text = string.Format($"A {XIVHuntNet} account, is required.", XIVDB.GameResources.GetWorldName(wid));
            else
                text = string.Format($"A {XIVHuntNet} account, with a verified character on {{0}}, is required.", XIVDB.GameResources.GetWorldName(wid));
            var link = new Hyperlink(new Run(XIVHuntNet))
            {
                NavigateUri = new Uri(AccountLoginUrl),
            };
            link.RequestNavigate += Link_RequestNavigate;
            var run1 = new Run(text.Substring(0, text.IndexOf(XIVHuntNet)));
            var run2 = new Run(text.Substring(text.IndexOf(XIVHuntNet) + XIVHuntNet.Length));
            InfoTextBlock.Inlines.Add(run1);
            InfoTextBlock.Inlines.Add(link);
            InfoTextBlock.Inlines.Add(run2);
        }

        internal static void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            LoginFailedTextBlock.Visibility = Visibility.Hidden;
            Cookie receivedCookie = null, twofaCookie = null;
            bool authresult = false;
            try
            {
                authresult = PasswordBox.Password.Length > 7 && AuthenticateUser(EmailTextBox.Text, PasswordBox.Password, TwoFABox.Password, out receivedCookie, out twofaCookie);
            }catch(Exception ex) { LogHost.Default.InfoException("An exception occured while trying to log in", ex); };
            if (authresult)
            {
                DialogResult = true;
                if (receivedCookie != null)
                    receivedCookies.Add(receivedCookie);
                if (twofaCookie != null)
                    receivedCookies.Add(twofaCookie);
                Settings.Default.Cookies = Convert.ToBase64String(ObjectToByteArray(receivedCookies));
                Settings.Default.Save();
                Close();
            }
            else
                LoginFailedTextBlock.Visibility = Visibility.Visible;
        }

        private static bool AuthenticateUser(string user, string password, string twofa, out Cookie authCookie, out Cookie twofaCookie)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(RemoteLoginUrl);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.CookieContainer = new CookieContainer();
            request.AllowAutoRedirect = false;
            var authCredentials = "Email=" + WebUtility.UrlEncode(user) + "&Password=" + WebUtility.UrlEncode(password) + "&RememberMe=true";
            if (!string.IsNullOrWhiteSpace(twofa))
            {
                authCredentials += "&TwoFactorCode=" + twofa;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(authCredentials);
            request.ContentLength = bytes.Length;
            using (var requestStream = request.GetRequestStream())
            {
                requestStream.Write(bytes, 0, bytes.Length);
            }
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                authCookie = response.Cookies[IdentityCookieName];
                if (authCookie == null && !string.IsNullOrWhiteSpace(twofa) && response.Cookies[TwoFactorUserIdCookieName] != null)
                {
                    TwoFA(twofa, response.Cookies[TwoFactorUserIdCookieName], out authCookie, out twofaCookie);
                }
                else
                    twofaCookie = null;
            }
            return authCookie != null;
        }

        private static bool TwoFA(string twofa, Cookie twofauseridcookie, out Cookie authCookie, out Cookie twofaCookie)
        {
            var request = WebRequest.Create(Login2FaUrl) as HttpWebRequest;
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.CookieContainer = new CookieContainer();
            request.CookieContainer.Add(twofauseridcookie);
            request.AllowAutoRedirect = false;
            var authCredentials = "TwoFactorCode=" + twofa + "&RememberMachine=true&RememberMe=true";
            byte[] bytes = Encoding.UTF8.GetBytes(authCredentials);
            request.ContentLength = bytes.Length;
            using (var requestStream = request.GetRequestStream())
            {
                requestStream.Write(bytes, 0, bytes.Length);
            }
            using (var response = request.GetResponse() as HttpWebResponse)
            {
                authCookie = twofaCookie = null;
                authCookie = response.Cookies[IdentityCookieName];
                twofaCookie = response.Cookies[TwoFactorRememberMeCookieName];
            }
            return authCookie != null && twofaCookie != null;
        }

        private static byte[] ObjectToByteArray(Object obj)
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }
    }
}
