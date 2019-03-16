﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace MCLauncher {
    class VersionDownloader {

        private HttpClient client = new HttpClient();
        private WUProtocol protocol = new WUProtocol();
        
        private async Task<XDocument> PostXmlAsync(string url, XDocument data) {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            using (var stringWriter = new StringWriter()) {
                using (XmlWriter xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { Indent = false, OmitXmlDeclaration = true })) {
                    data.Save(xmlWriter);
                }
                request.Content = new StringContent(stringWriter.ToString(), Encoding.UTF8, "application/soap+xml");
            }
            using (var resp = await client.SendAsync(request)) {
                string str = await resp.Content.ReadAsStringAsync();
                return XDocument.Parse(str);
            }
        }

        private async Task DownloadFile(string url, string to, DownloadProgress progress, CancellationToken cancellationToken) {
            using (var resp = await client.GetAsync(url, cancellationToken)) {
                using (var inStream = await resp.Content.ReadAsStreamAsync())
                using (var outStream = new FileStream(to, FileMode.Create)) {
                    long? totalSize = resp.Content.Headers.ContentLength;
                    long transferred = 0;
                    byte[] buf = new byte[1024 * 1024];
                    while (true) {
                        int n = await inStream.ReadAsync(buf, 0, buf.Length, cancellationToken);
                        if (n == 0)
                            break;
                        await outStream.WriteAsync(buf, 0, n, cancellationToken);
                        transferred += n;
                        progress(transferred, totalSize);
                    }
                }
            }
        }

        private async Task<string> GetDownloadUrl(string updateIdentity, string revisionNumber) {
            XDocument result = await PostXmlAsync(protocol.GetDownloadUrl(),
                protocol.BuildDownloadRequest(updateIdentity, revisionNumber));
            foreach (string s in protocol.ExtractDownloadResponseUrls(result)) {
                if (s.StartsWith("http://tlu.dl.delivery.mp.microsoft.com/"))
                    return s;
            }
            return null;
        }

        public async Task Download(string updateIdentity, string revisionNumber, DownloadProgress progress, CancellationToken cancellationToken) {
            string link = await GetDownloadUrl(updateIdentity, revisionNumber);
            if (link == null)
                throw new ArgumentException("Bad updateIdentity");
            Debug.WriteLine("Resolved download link: " + link);
            await DownloadFile(link, updateIdentity + ".Appx", progress, cancellationToken);
        }

        public delegate void DownloadProgress(long current, long? total);

    }
}