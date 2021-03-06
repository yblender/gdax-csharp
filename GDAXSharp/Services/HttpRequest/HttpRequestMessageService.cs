﻿using System;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using GDAXSharp.Utilities.Extensions;
using GDAXSharp.Authentication;
using GDAXSharp.Utilities;

namespace GDAXSharp.Services.HttpRequest
{
    public class HttpRequestMessageService : IHttpRequestMessageService
    {
        private const string apiUri = "https://api.gdax.com";

        private const string sandBoxApiUri = "https://api-public.sandbox.gdax.com";

        private readonly IClock clock;

        private readonly bool sandBox;

        public HttpRequestMessageService(IClock clock, bool sandBox)
        {
            this.clock = clock;
            this.sandBox = sandBox;
        }

        public HttpRequestMessage CreateHttpRequestMessage(
            HttpMethod httpMethod, 
            IAuthenticator authenticator, 
            string requestUri, 
            string contentBody = "")
        {
            var baseUri = sandBox
                ? sandBoxApiUri
                : apiUri;

            var requestMessage = new HttpRequestMessage(httpMethod, new Uri(new Uri(baseUri), requestUri))
            {
                Content = contentBody == string.Empty
                    ? null
                    : new StringContent(contentBody, Encoding.UTF8, "application/json")
            };

            var timeStamp = clock.GetTime().ToTimeStamp();
            var signedSignature = ComputeSignature(httpMethod, authenticator.UnsignedSignature, timeStamp, requestUri, contentBody);

            AddHeaders(requestMessage, authenticator, signedSignature, timeStamp);
            return requestMessage;
        }

        private string ComputeSignature(HttpMethod httpMethod, string secret, double timestamp, string requestUri, string contentBody = "")
        {
            var convertedString = Convert.FromBase64String(secret);
            var prehash = timestamp.ToString("F0", CultureInfo.InvariantCulture) + httpMethod.ToString().ToUpper() + requestUri + contentBody;
            return HashString(prehash, convertedString);
        }

        private string HashString(string str, byte[] secret)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            using (var hmaccsha = new HMACSHA256(secret))
            {
                return Convert.ToBase64String(hmaccsha.ComputeHash(bytes));
            }
        }

        private void AddHeaders(
            HttpRequestMessage httpRequestMessage,
            IAuthenticator authenticator,
            string signedSignature,
            double timeStamp)
        {
            httpRequestMessage.Headers.Add("User-Agent", "GDAXClient");
            httpRequestMessage.Headers.Add("CB-ACCESS-KEY", authenticator.ApiKey);
            httpRequestMessage.Headers.Add("CB-ACCESS-TIMESTAMP", timeStamp.ToString("F0", CultureInfo.InvariantCulture));
            httpRequestMessage.Headers.Add("CB-ACCESS-SIGN", signedSignature);
            httpRequestMessage.Headers.Add("CB-ACCESS-PASSPHRASE", authenticator.Passphrase);
        }
    }
}
