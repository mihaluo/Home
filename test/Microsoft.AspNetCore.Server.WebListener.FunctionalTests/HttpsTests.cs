// Copyright (c) Microsoft Open Technologies, Inc.
// All Rights Reserved
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING
// WITHOUT LIMITATION ANY IMPLIED WARRANTIES OR CONDITIONS OF
// TITLE, FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABLITY OR
// NON-INFRINGEMENT.
// See the Apache 2 License for the specific language governing
// permissions and limitations under the License.

using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Microsoft.AspNetCore.Server.WebListener
{
    public class HttpsTests
    {
        private const string Address = "https://localhost:9090/";

        [Fact(Skip = "TODO: Add trait filtering support so these SSL tests don't get run on teamcity or the command line."), Trait("scheme", "https")]
        public async Task Https_200OK_Success()
        {
            using (Utilities.CreateHttpsServer(httpContext =>
            {
                return Task.FromResult(0);
            }))
            {
                string response = await SendRequestAsync(Address);
                Assert.Equal(string.Empty, response);
            }
        }

        [Fact(Skip = "TODO: Add trait filtering support so these SSL tests don't get run on teamcity or the command line."), Trait("scheme", "https")]
        public async Task Https_SendHelloWorld_Success()
        {
            using (Utilities.CreateHttpsServer(httpContext =>
            {
                byte[] body = Encoding.UTF8.GetBytes("Hello World");
                httpContext.Response.ContentLength = body.Length;
                return httpContext.Response.Body.WriteAsync(body, 0, body.Length);
            }))
            {
                string response = await SendRequestAsync(Address);
                Assert.Equal("Hello World", response);
            }
        }

        [Fact(Skip = "TODO: Add trait filtering support so these SSL tests don't get run on teamcity or the command line."), Trait("scheme", "https")]
        public async Task Https_EchoHelloWorld_Success()
        {
            using (Utilities.CreateHttpsServer(httpContext =>
            {
                string input = new StreamReader(httpContext.Request.Body).ReadToEnd();
                Assert.Equal("Hello World", input);
                byte[] body = Encoding.UTF8.GetBytes("Hello World");
                httpContext.Response.ContentLength = body.Length;
                httpContext.Response.Body.Write(body, 0, body.Length);
                return Task.FromResult(0);
            }))
            {
                string response = await SendRequestAsync(Address, "Hello World");
                Assert.Equal("Hello World", response);
            }
        }

        [Fact(Skip = "TODO: Add trait filtering support so these SSL tests don't get run on teamcity or the command line."), Trait("scheme", "https")]
        public async Task Https_ClientCertNotSent_ClientCertNotPresent()
        {
            using (Utilities.CreateHttpsServer(async httpContext =>
            {
                var tls = httpContext.Features.Get<ITlsConnectionFeature>();
                Assert.NotNull(tls);
                var cert = await tls.GetClientCertificateAsync(CancellationToken.None);
                Assert.Null(cert);
                Assert.Null(tls.ClientCertificate);
            }))
            {
                string response = await SendRequestAsync(Address);
                Assert.Equal(string.Empty, response);
            }
        }

        [Fact(Skip = "TODO: Add trait filtering support so these SSL tests don't get run on teamcity or the command line."), Trait("scheme", "https")]
        public async Task Https_ClientCertRequested_ClientCertPresent()
        {
            using (Utilities.CreateHttpsServer(async httpContext =>
            {
                var tls = httpContext.Features.Get<ITlsConnectionFeature>();
                Assert.NotNull(tls);
                var cert = await tls.GetClientCertificateAsync(CancellationToken.None);
                Assert.NotNull(cert);
                Assert.NotNull(tls.ClientCertificate);
            }))
            {
                X509Certificate2 cert = FindClientCert();
                Assert.NotNull(cert);
                string response = await SendRequestAsync(Address, cert);
                Assert.Equal(string.Empty, response);
            }
        }

        private async Task<string> SendRequestAsync(string uri, 
            X509Certificate cert = null)
        {
#if DNX451
            var handler = new WebRequestHandler();
#else
            var handler = new WinHttpHandler();
#endif
            handler.ServerCertificateValidationCallback = (a, b, c, d) => true;
            if (cert != null)
            {
                handler.ClientCertificates.Add(cert);
            }
            using (HttpClient client = new HttpClient(handler))
            {
                return await client.GetStringAsync(uri);
            }
        }

        private async Task<string> SendRequestAsync(string uri, string upload)
        {
#if DNX451
            var handler = new WebRequestHandler();
#else
            var handler = new WinHttpHandler();
#endif
            handler.ServerCertificateValidationCallback = (a, b, c, d) => true;
            using (HttpClient client = new HttpClient(handler))
            {
                HttpResponseMessage response = await client.PostAsync(uri, new StringContent(upload));
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
        }

        private X509Certificate2 FindClientCert()
        {
            var store = new X509Store();
            store.Open(OpenFlags.ReadOnly);

            foreach (var cert in store.Certificates)
            {
                bool isClientAuth = false;
                bool isSmartCard = false;
                foreach (var extension in cert.Extensions)
                {
                    var eku = extension as X509EnhancedKeyUsageExtension;
                    if (eku != null)
                    {
                        foreach (var oid in eku.EnhancedKeyUsages)
                        {
                            if (oid.FriendlyName == "Client Authentication")
                            {
                                isClientAuth = true;
                            }
                            else if (oid.FriendlyName == "Smart Card Logon")
                            {
                                isSmartCard = true;
                                break;
                            }
                        }
                    }
                }

                if (isClientAuth && !isSmartCard)
                {
                    return cert;
                }
            }
            return null;
        }
    }
}
