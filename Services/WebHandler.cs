using Discord;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YoutubeExplode.Videos;

namespace DownloadBot.Services
{
    public class WebHandler
    {
        public bool isURI(string url)
        {
            // check if string is a URL
            return Uri.TryCreate(url, UriKind.Absolute, out Uri uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttps);
        }

        public bool isYoutube(string url)
            => Regex.Match(url, @"(https?:\/\/|)(www\.|)?(youtube.com/(shorts/|watch\?v?)|youtu.be/)").Success;

        public bool isReddit(string url)
            => Regex.Match(url, @"https?:\/\/(www\.)?reddit.com/r/").Success;

        public int GetContentLength(string url)
        {
            var httpClient = new HttpClient();
            var httpResponse = httpClient.GetAsync(url).Result;
            int length = int.Parse(httpResponse.Content.Headers.First(h => h.Key.Equals("Content-Length")).Value.First());

            return length;
        }

        public string? DownloadString(string? url, int length = -1)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            var httpClient = new HttpClient();
            var httpResponse = httpClient.GetAsync(url).Result;
            var content = httpResponse.Content.ReadAsStringAsync().Result;

            return content;
        }

        public Stream? DownloadStream(string? url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            var httpClient = new HttpClient();
            var httpResponse = httpClient.GetAsync(url).Result;
            var content = httpResponse.Content.ReadAsStreamAsync().Result;

            return content;
        }
    }
}
