using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace XIVAPI
{
    public class Pagination
    {
        public int Page { get; set; }
        public int PageTotal { get; set; }
        public int Results { get; set; }
        public int ResultsPerPage { get; set; }
        public int ResultsTotal { get; set; }
    }

    public class Item
    {
        public string Name { get; set; }
        public byte Rarity { get; set; }
        public uint ID { get; set; }
        public string Url { get; set; }
        public bool CanBeHq { get; set; }
    }

    class XIVAPIClient
    {
        private const int MaximumRequestPerSecond = 20;
        private static readonly TimeSpan msBetweenEachRequest = TimeSpan.FromMilliseconds(1000 / MaximumRequestPerSecond);
        private static DateTime LastCall = DateTime.MinValue;
        private readonly HttpClient http = new HttpClient { BaseAddress = new Uri("https://xivapi.com/") };

        private async Task<HttpResponseMessage> GetAsync(string url)
        {
            while (LastCall > DateTime.UtcNow.Subtract(msBetweenEachRequest))
                await Task.Delay(msBetweenEachRequest);
            HttpResponseMessage r = await http.GetAsync(url);
            LastCall = DateTime.UtcNow;
            return r;
        }

        public async Task<Item> SearchForItem(string itemName, bool detailed = false, uint page = 1)
        {
            HttpResponseMessage r = await GetAsync($"/search?string={WebUtility.UrlEncode(itemName)}&index=item&page={page}&string_algo=query_string&language={Thread.CurrentThread.CurrentUICulture.Name.Substring(0, 2)}");
            if (!r.IsSuccessStatusCode)
                return null;
            JObject jObject = JObject.Parse(await r.Content.ReadAsStringAsync());
            Pagination pagination = jObject.SelectToken("Pagination").ToObject<Pagination>();
            List<Item> results = jObject.SelectToken("Results").ToObject<List<Item>>();
            if (results.Any(x => x.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase)))
            {
                Item result = results.SingleOrDefault(x => x.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));
                if (detailed)
                {
                    r = await GetAsync(result.Url);
                    if(r.IsSuccessStatusCode)
                        result.CanBeHq = JObject.Parse(await r.Content.ReadAsStringAsync()).ToObject<Item>().CanBeHq;
                }
                return result;
            }
            if (page < pagination.PageTotal)
                return await SearchForItem(itemName, detailed, page + 1);
            return null;
        }
    }
}
