using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Limitation;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace AzureaProxy
{
    public partial class Form1 : Form
    {
        static string ct;
        static string cs;
        static string ut;
        static string us;

        public Form1()
        {
            InitializeComponent();
        }

        private void 종료ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private bool m_shown = false;
        private void Form1_Shown(object sender, EventArgs e)
        {
            if (this.m_shown) return;
            this.m_shown = true;
            
            if (File.Exists("token.txt"))
            {
                var ff = File.ReadAllLines("token.txt");

                ct = ff[0];
                cs = ff[1];
                ut = ff[2];
                us = ff[3];

                this.Left = -1000;
                this.Top = -1000;
                this.StartProxy();
            }
            else
            {
                if (!RequestToken())
                {
                    MessageBox.Show("오류가 발생하였습니다.");
                    Application.Exit();
                    return;
                }
                Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = "https://api.twitter.com/oauth/authorize?oauth_token=" + ut });
            }
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            this.textBox1.Enabled = false;
            if (!AccessToken(this.textBox1.Text))
            {
                MessageBox.Show("오류가 발생하였습니다.");
                Application.Exit();
                return;
            }

            File.WriteAllLines("token.txt", new string[] { ut, us });

            this.StartProxy();
        }

        static bool RequestToken()
        {
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("Authorization", OAuth.GenerateAuthorization(ct, cs, null, null, "POST", "https://api.twitter.com/oauth/request_token", null));

                    var r = wc.UploadString("https://api.twitter.com/oauth/request_token", "");

                    ut = Regex.Match(r, @"oauth_token=([^&]+)").Groups[1].Value;
                    us = Regex.Match(r, @"oauth_token_secret=([^&]+)").Groups[1].Value;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool AccessToken(string verifier)
        {
            try
            {
                var obj = new { oauth_verifier = verifier };
                var buff = OAuth.ToString(obj);

                using (var wc = new WebClient())
                {
                    wc.Headers.Add("Authorization", OAuth.GenerateAuthorization(ct, cs, ut, us, "POST", "https://api.twitter.com/oauth/access_token", obj));
                    wc.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                    var r = wc.UploadString("https://api.twitter.com/oauth/access_token", buff);

                    ut = Regex.Match(r, @"oauth_token=([^&]+)").Groups[1].Value;
                    us = Regex.Match(r, @"oauth_token_secret=([^&]+)").Groups[1].Value;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        void StartProxy()
        {
            this.Visible = false;
            this.Hide();
            this.notifyIcon1.Visible = true;
            this.notifyIcon1.ShowBalloonTip(10000, "아즈레아 산소호흡기", "종료하시려면 더블클릭하세요.", ToolTipIcon.Info);

            var sr = new ProxyServer();
            sr.TrustRootCertificate = true;
            sr.BeforeRequest += OnRequest;
            sr.ServerCertificateValidationCallback += (s, e) => { e.IsValid = true; return Task.FromResult(0); };
            sr.ClientCertificateSelectionCallback += (s, e) => Task.FromResult(0);

            var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Loopback, 8080, true);
            sr.AddEndPoint(explicitEndPoint);
            
#if DEBUG
            sr.UpStreamHttpProxy  = sr.UpStreamHttpsProxy = new ExternalProxy { HostName = "localhost", Port = 8888 };
#endif
            sr.Start();
        }
        
        public static async Task OnRequest(object sender, SessionEventArgs e)
        {
            var requestHeaders = e.WebSession.Request.Headers;

            //if (requestHeaders.HeaderExists("Authorization") && requestHeaders.Headers["Authorization"].Value.Contains("SyFYmZ2qa9eI9p7sheZFlw"))
            var headerExists = requestHeaders.HeaderExists("Authorization");
            if (headerExists || e.WebSession.Request.Url.Contains("oauth_signature"))
            {
                string bodyString = null;

                if (e.WebSession.Request.HasBody)
                    bodyString = await e.GetRequestBodyAsString();
                
                var newAuthorization = 
                    OAuth.GenerateAuthorization(
                        ct,
                        cs,
                        ut,
                        us,
                        e.WebSession.Request.Method,
                        e.WebSession.Request.Url,
                        bodyString);

                if (headerExists)
                    e.WebSession.Request.Headers.Headers["Authorization"].Value = newAuthorization;
                else
                    e.WebSession.Request.Headers.AddHeader("Authorization", newAuthorization);

                if (e.WebSession.Request.HasBody)
                {
                    await e.SetRequestBodyString(bodyString);

                    var bodyBytes = await e.GetRequestBody();

                    await e.SetRequestBody(bodyBytes);
                }
            }
            else if (e.WebSession.Request.HasBody)
            {
                var bodyString = await e.GetRequestBodyAsString();
                await e.SetRequestBodyString(bodyString);

                var bodyBytes = await e.GetRequestBody();
                await e.SetRequestBody(bodyBytes);

            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Application.Exit();
        }
    }
}
