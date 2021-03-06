﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using GDAXSharp.Utilities.Extensions;
using GDAXSharp.Authentication;
using GDAXSharp.HttpClient;
using GDAXSharp.Services.HttpRequest;
using GDAXSharp.Services.Products.Models;
using GDAXSharp.Services.Products.Models.Responses;
using GDAXSharp.Shared;
using GDAXSharp.Utilities;

namespace GDAXSharp.Services.Products
{
    public class ProductsService : AbstractService
    {
        private readonly IHttpClient httpClient;

        private readonly IAuthenticator authenticator;

        private readonly IQueryBuilder queryBuilder;

        public ProductsService(
            IHttpClient httpClient,
            IHttpRequestMessageService httpRequestMessageService,
            IAuthenticator authenticator,
            IQueryBuilder queryBuilder)
                : base(httpClient, httpRequestMessageService)
        {
            this.httpClient = httpClient;
            this.authenticator = authenticator;
            this.queryBuilder = queryBuilder;
        }

        public async Task<IEnumerable<Product>> GetAllProductsAsync()
        {
            var httpResponseMessage = await SendHttpRequestMessageAsync(HttpMethod.Get, authenticator, "/products");
            var contentBody = await httpClient.ReadAsStringAsync(httpResponseMessage).ConfigureAwait(false);
            var productsResponse = DeserializeObject<IEnumerable<Product>>(contentBody);

            return productsResponse;
        }

        public async Task<ProductsOrderBookResponse> GetProductOrderBookAsync(
			ProductType productId, 
			ProductLevel productLevel = ProductLevel.One)
        {
            var httpResponseMessage = await SendHttpRequestMessageAsync(HttpMethod.Get, authenticator, $"/products/{productId.GetEnumMemberValue()}/book/?level={(int)productLevel}");
            var contentBody = await httpClient.ReadAsStringAsync(httpResponseMessage).ConfigureAwait(false);
            var productsOrderBookJsonResponse = DeserializeObject<ProductsOrderBookJsonResponse>(contentBody);

            var productOrderBookResponse = ConvertProductOrderBookResponse(productsOrderBookJsonResponse, productLevel);

            return productOrderBookResponse;
        }

        public async Task<ProductTicker> GetProductTickerAsync(ProductType productId)
        {
            var httpResponseMessage = await SendHttpRequestMessageAsync(HttpMethod.Get, authenticator, $"/products/{productId.GetEnumMemberValue()}/ticker");
            var contentBody = await httpClient.ReadAsStringAsync(httpResponseMessage).ConfigureAwait(false);
            var productTickerResponse = DeserializeObject<ProductTicker>(contentBody);

            return productTickerResponse;
        }

        public async Task<ProductStats> GetProductStatsAsync(ProductType productId)
        {
            var httpResponseMessage = await SendHttpRequestMessageAsync(HttpMethod.Get, authenticator, $"/products/{productId.GetEnumMemberValue()}/stats");
            var contentBody = await httpClient.ReadAsStringAsync(httpResponseMessage).ConfigureAwait(false);
            var productStatsResponse = DeserializeObject<ProductStats>(contentBody);

            return productStatsResponse;
        }

        public async Task<IList<IList<ProductTrade>>> GetTradesAsync(
            ProductType productId,
            int limit = 100,
            int numberOfPages = 0)
        {
            var httpResponseMessage = await SendHttpRequestMessagePagedAsync<ProductTrade>(HttpMethod.Get, authenticator, $"/products/{productId.GetEnumMemberValue()}/trades?limit={limit}", numberOfPages: numberOfPages);

            return httpResponseMessage;
        }

        public async Task<IList<Candle>> GetHistoricRatesAsync(
			ProductType productPair, 
			DateTime start, 
			DateTime end, 
			CandleGranularity granularity)
        {
            const int maxPeriods = 300;

            var rc = new List<Candle>();

            DateTime? batchEnd = end;
            DateTime batchStart;

            var maxBatchPeriod = (int)granularity * maxPeriods;

            do
            {
                if (batchEnd == null) {
                    break;
                }

                batchStart = batchEnd.Value.AddSeconds(-maxBatchPeriod);
                if (batchStart < start) batchStart = start;

                rc.AddRange(await GetHistoricRatesAsync(productPair, batchStart, batchEnd.Value, (int)granularity));

                batchEnd = rc.Last()?.Time;
            } while (batchStart > start);

            return rc;
        }

        private async Task<IList<Candle>> GetHistoricRatesAsync(
			ProductType productId, 
			DateTime start, 
			DateTime end, 
			int granularity)
        {
            var isoStart = start.ToString("s");
            var isoEnd = end.ToString("s");

            var queryString = queryBuilder.BuildQuery(
                new KeyValuePair<string, string>("start", isoStart),
                new KeyValuePair<string, string>("end", isoEnd),
                new KeyValuePair<string, string>("granularity", granularity.ToString()));

            var httpResponseMessage = await SendHttpRequestMessageAsync(HttpMethod.Get, authenticator, $"/products/{productId.GetEnumMemberValue()}/candles" + queryString);
            var contentBody = await httpClient.ReadAsStringAsync(httpResponseMessage).ConfigureAwait(false);
            var productHistoryResponse = DeserializeObject<IList<Candle>>(contentBody);

            return productHistoryResponse;
        }

        private ProductsOrderBookResponse ConvertProductOrderBookResponse(
            ProductsOrderBookJsonResponse productsOrderBookJsonResponse,
            ProductLevel productLevel)
        {
            var askList = productsOrderBookJsonResponse.Asks.Select(product => product.ToArray()).Select(askArray => new Ask(Convert.ToDecimal(askArray[0], CultureInfo.InvariantCulture), Convert.ToDecimal(askArray[1], CultureInfo.InvariantCulture))
            {
                OrderId = productLevel == ProductLevel.Three
                    ? new Guid(askArray[2])
                    : (Guid?)null,
                NumberOfOrders = productLevel == ProductLevel.Three
                    ? (decimal?)null
                    : Convert.ToDecimal(askArray[2], CultureInfo.InvariantCulture)
            }).ToArray();

            var bidList = productsOrderBookJsonResponse.Bids.Select(product => product.ToArray()).Select(bidArray => new Bid(Convert.ToDecimal(bidArray[0], CultureInfo.InvariantCulture), Convert.ToDecimal(bidArray[1], CultureInfo.InvariantCulture))
            {
                OrderId = productLevel == ProductLevel.Three
                    ? new Guid(bidArray[2])
                    : (Guid?)null,
                NumberOfOrders = productLevel == ProductLevel.Three
                    ? (decimal?)null
                    : Convert.ToDecimal(bidArray[2], CultureInfo.InvariantCulture)
            });

            var productOrderBookResponse = new ProductsOrderBookResponse(productsOrderBookJsonResponse.Sequence, bidList, askList);
            return productOrderBookResponse;
        }
    }
}
