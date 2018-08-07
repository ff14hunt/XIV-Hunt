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
        public int Page_total { get; set; }
        public int Results { get; set; }
        public int Results_per_page { get; set; }
        public int Results_total { get; set; }
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
        private static TimeSpan msBetweenEachRequest = TimeSpan.FromMilliseconds(1000 / MaximumRequestPerSecond);
        private static DateTime LastCall = DateTime.MinValue;
        private readonly HttpClient http = new HttpClient { BaseAddress = new Uri("https://xivapi.com/") };

        private async Task<HttpResponseMessage> GetAsync(string url)
        {
            while (LastCall > DateTime.UtcNow.Subtract(msBetweenEachRequest))
                await Task.Delay((int)msBetweenEachRequest.TotalMilliseconds);
            HttpResponseMessage r = await http.GetAsync(url);
            LastCall = DateTime.UtcNow;
            return r;
        }

        public async Task<Item> SearchForItem(string itemName, bool detailed = false, uint page = 1)
        {
            HttpResponseMessage r = await GetAsync($"/search?string={WebUtility.UrlEncode(itemName)}&index=item{page}&string_algo=multi_match&language={Thread.CurrentThread.CurrentUICulture.Name.Substring(0, 2)}");
            if (!r.IsSuccessStatusCode)
                return null;
            JObject jObject = JObject.Parse(await r.Content.ReadAsStringAsync());
            Pagination pagination = jObject.SelectToken("pagination").ToObject<Pagination>();
            List<Item> results = jObject.SelectToken("results").ToObject<List<Item>>();
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
            if (page < pagination.Page_total)
                return await SearchForItem(itemName, detailed, page + 1);
            return null;
        }
    }
}
