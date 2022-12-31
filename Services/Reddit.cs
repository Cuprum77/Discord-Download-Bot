using Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace DownloadBot.Services
{
    public struct RedditVideo
    {
        public string Url { get; set; }
        public int Bandwidth { get; set; }
        public int Resolution { get; set; }
        public int Framerate { get; set; }
        public string? AudioUrl { get; set; }

        public override string ToString()
            => $"""
            Url: {Url}
            Bandwidth: {Bandwidth} bits
            Resolution: {Resolution}p
            Framerate: {Framerate} fps
            Audio Url: {AudioUrl}
            """;
    }

    public struct RedditImage
    {
        public string? Url { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }

        public override string ToString()
            => $"""
            Url: {Url}
            Width: {Width}
            Height: {Height}
            """;
    }

    public struct RedditAward
    {
        public string? Name { get; set; }
        public int? CoinPrice { get; set; }
        public int? CoinReward { get; set; }
        public int? Count { get; set; }
        public int? DaysOfPremium { get; set; }

        public override string ToString()
            => $"""
            Name: {Name}
            Coin Price: {CoinPrice}
            Coin Reward: {CoinReward}
            Number of Coins: {Count}
            Days of Premium (per coin): {DaysOfPremium}
            """;
    }

    public class RedditAwards
    {
        public List<RedditAward> Awards { get; set; }

        public int TotalCost()
        {
            int cost = 0;
            foreach (var award in Awards)
                cost += (award.CoinPrice ?? 0) * (award.Count ?? 0);

            return cost;
        }

        public int TotalReward()
        {
            int reward = 0;
            foreach (var award in Awards)
                reward += (award.CoinReward ?? 0) * (award.Count ?? 0);

            return reward;
        }

        public int TotalCount()
        {
            int count = 0;
            foreach (var award in Awards)
                count += award.Count ?? 0;

            return count;
        }

        public int TotalDaysPremium()
        {
            int days = 0;
            foreach (var award in Awards)
                days += (award.DaysOfPremium ?? 0) * (award.Count ?? 0);

            return days;
        }
    }

    public struct RedditUser
    {
        public string? Username { get; set; }
        public string? Url { get; set; }

        public override string ToString()
        => $"""
            Username: {Username}
            Url: {Url}
            """;
    }

    public struct RedditPost
    {
        public string? Title { get; set; }
        public string? Url { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? FlairText { get; set; }

        public int? CreatedAt { get; set; }
        public int? CreatedAtUTC { get; set; }

        public bool? Archived { get; set; }
        public bool? Quarantined { get; set; }
        public bool? AgeRestricted { get; set; }

        public int? Upvotes { get; set; }
        public int? Gilded { get; set; }
        public int? CommentsCount { get; set; }
        public int? CrosspostCount { get; set; }
        public int? DuplicateCount { get; set; }

        public override string ToString()
            => $"""
            Title: {Title}
            Url: {Url}
            Thumbnail Url: {ThumbnailUrl}
            Flair Text: {FlairText}
            Created at: {CreatedAt}
            Created at UTC: {CreatedAtUTC}
            Archived: {Archived}
            Quarantined: {Quarantined}
            Age Restricted: {AgeRestricted}
            Upvotes: {Upvotes}
            Gilded: {Gilded}
            Number of Comments: {CommentsCount}
            Number og Crossposts: {CrosspostCount}
            Number of Duplicates: {DuplicateCount}
            """;
    }

    public struct RedditSubreddit
    {
        public string? Name { get; set; }
        public string? Url { get; set; }
        public string? IconUrl { get; set; }
        public string? BannerUrl { get; set; }
        public int? Subscribers { get; set; }
        public int? ActiveUsers { get; set; }

        public override string ToString()
            => $"""
            Name: {Name}
            Url: {Url}
            Icon Url: {IconUrl}
            Banner Url: {BannerUrl}
            Subscribers: {Subscribers}
            ActiveUsers: {ActiveUsers}
            """;
    }

    public class Reddit
    {
        string url = "";
        string dashUrl = "";
        int limit = 8000000;

        List<RedditVideo> videos = new List<RedditVideo>();
        List<RedditImage> images = new List<RedditImage>();
        RedditAwards redditAwards = new RedditAwards();
        RedditUser user = new RedditUser();
        RedditPost post = new RedditPost();
        RedditSubreddit subreddit = new RedditSubreddit();

        public Reddit(string url, int discordLimit = 8000000)
        {
            Url = url;
            limit = discordLimit;
        }

        public string Url
        {
            get => url;
            set
            {
                // grab the webhandler object
                var webHandler = new WebHandler();

                // set the URL if its a valid reddit url, if not, throw an error
                if (webHandler.isReddit(value))
                    url = value;
                else
                    throw new Exception("Invalid URL");
            }
        }

        public string Filename { get; private set; }
        public RedditVideo Video { get; private set; }
        public List<RedditImage> Image { get; private set; }
        public RedditAwards Awards { get; private set; }
        public RedditUser User { get; private set; }
        public RedditPost Post { get; private set; }
        public RedditSubreddit Subreddit { get; private set; }

        public void DownloadMetaData()
        {
            // i tried playing nice and using their official API token, but holy shit
            // they do not make it easy to use, so im gonna go back to scraping
            // create a webhandler object, and grab the json file
            var webhandler = new WebHandler();
            // remove anything after a question mark
            var cleanUrl = Regex.Replace(Url, @"\?(.*)", "");
            // append the json request onto the link
            cleanUrl += ".json?limit=1";
            var jsonFile = webhandler.DownloadString(cleanUrl);

            // if the file is empty for some reason, throw an exception
            if (string.IsNullOrEmpty(jsonFile))
                throw new Exception("Empty JSON!");

            // deserialize the json
            var json = JArray.Parse(jsonFile);
            if(!json.HasValues)
                throw new Exception("Empty JSON!");

            // remove everything but the actual post data
            var postJson = json[0]["data"]["children"];
            postJson = postJson[0]["data"];

            // to satisfy the compiler, we will do a sanity check for null
            if (postJson == null)
                throw new Exception("Empty JSON!");

            dashUrl = postJson["media"]["reddit_video"]["dash_url"].ToString();
            // if its not null, remove the query string
            if (!string.IsNullOrEmpty(dashUrl))
                dashUrl = Regex.Replace(dashUrl, @"\?(.*)", "");

            // if theres images in the metadata, then extract eeach image resolution
            try
            {
                if (postJson["preview"]["images"].HasValues)
                {
                    // create a variable for holding the location
                    var imageJson = postJson["preview"]["images"];

                    // loop through each image
                    foreach (var image in imageJson[0]["resolutions"])
                    {
                        // create a new reddit image object
                        RedditImage redditImage = new RedditImage();
                        // grab the source url
                        redditImage.Url = (image["url"] ?? "").ToString();
                        // grab the width and height
                        redditImage.Width = image["width"].ToObject<int?>() ?? 0;
                        redditImage.Height = image["height"].ToObject<int?>() ?? 0;
                        // add the image to the list
                        images.Add(redditImage);
                    }
                    // extract the source image and add it to the list
                    RedditImage sourceImage = new RedditImage();
                    sourceImage.Url = (imageJson[0]["source"]["url"] ?? "").ToString();
                    sourceImage.Width = imageJson[0]["source"]["width"].ToObject<int?>() ?? 0;
                    sourceImage.Height = imageJson[0]["source"]["height"].ToObject<int?>() ?? 0;
                    images.Add(sourceImage);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                Image = images;
            }

            // grab the post metadata from the json
            try
            {
                post.Title = (postJson["title"] ?? "").ToString();
                post.Url = (postJson["url"] ?? "").ToString();
                post.ThumbnailUrl = (postJson["thumbnail"] ?? "").ToString();
                post.FlairText = (postJson["link_flair_text"] ?? "").ToString();
                post.CreatedAt = postJson["created"].ToObject<int?>() ?? 0;
                post.CreatedAtUTC = postJson["created_utc"].ToObject<int?>() ?? 0;
                post.Archived = (postJson["archived"] ?? false).ToObject<bool>();
                post.Quarantined = (postJson["quarantine"] ?? false).ToObject<bool>();
                post.AgeRestricted = (postJson["over_18"] ?? false).ToObject<bool>();
                post.Upvotes = postJson["ups"].ToObject<int?>() ?? 0;
                post.Gilded = postJson["gilded"].ToObject<int?>() ?? 0;
                post.CommentsCount = postJson["num_comments"].ToObject<int?>() ?? 0;
                post.CrosspostCount = postJson["num_crossposts"].ToObject<int?>() ?? 0;
                post.DuplicateCount = postJson["num_duplicates"].ToObject<int?>() ?? 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                Post = post;
            }

            try
            {
                user.Username = (postJson["author"] ?? "").ToString();
                user.Url = "https://www.reddit.com/user/" + user.Username + "/";
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                User = user;
            }

            // loop through each award in "all_awardings" if it exists, and extract each award
            try
            {
                if (postJson["all_awardings"].HasValues)
                {
                    var awardList = new List<RedditAward>();

                    foreach (var award in postJson["all_awardings"])
                    {
                        var awardData = new RedditAward();
                        awardData.Name = (award["name"] ?? "").ToString();
                        awardData.CoinPrice = award["coin_price"].ToObject<int?>() ?? 0;
                        awardData.CoinReward = award["coin_reward"].ToObject<int?>() ?? 0;
                        awardData.Count = award["count"].ToObject<int?>() ?? 0;
                        awardData.DaysOfPremium = award["days_of_premium"].ToObject<int?>() ?? 0;
                        awardList.Add(awardData);
                    }

                    redditAwards.Awards = awardList;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                Awards = redditAwards;
            }

            try
            {
                if (postJson["subreddit"] != null) subreddit.Name = (postJson["subreddit"] ?? "").ToString();
                subreddit.Url = "https://www.reddit.com/r/" + subreddit.Name + "/";

                // extract the subreddit stats from its about me page
                var aboutUrl = subreddit.Url + "about/.json?limit=1";
                var jsonAboutFile = webhandler.DownloadString(aboutUrl);
                var aboutJson = JObject.Parse(jsonAboutFile);
                if (!aboutJson.HasValues)
                    throw new Exception("Empty About Json!");

                // grab the remaining subreddit metadata from here
                subreddit.Subscribers = aboutJson["data"]["subscribers"].ToObject<int?>() ?? 0;
                subreddit.ActiveUsers = aboutJson["data"]["accounts_active"].ToObject<int?>() ?? 0;
                subreddit.IconUrl = (aboutJson["data"]["icon_img"] ?? "").ToString();
                subreddit.BannerUrl = (aboutJson["data"]["banner_background_image"] ?? "").ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                Subreddit = subreddit;
            }
        }

        public void DownloadVideoData()
        {
            // create a webhandler object, and grab the json file
            var webhandler = new WebHandler();
            // check if the dashurl exists
            if (string.IsNullOrEmpty(dashUrl))
                throw new Exception("Dash URL is empty!");
            var mpd = webhandler.DownloadString(dashUrl);

            // check if video contains audio
            bool hasAudio = mpd.Contains("DASH_audio.mp4");
            bool oldAudio = mpd.Contains("audio") && !mpd.Contains("DASH_audio.mp4");
            bool hasExtension = mpd.Contains(".mp4");

            // grab metadata using regex
            var bandwidth = Regex.Matches(mpd, @"(?<=Representation bandwidth="")(.*)(?="" c)").ToList();
            var baseUrl = Regex.Matches(mpd, @"(?<=BaseURL>)((DASH_220|DASH_240|DASH_360|DASH_480|DASH_720|DASH_1080)*)(?=(\.mp4<|<))").ToList();
            var framerate = Regex.Matches(mpd, @"(?<=frameRate="")(.*)(?="" h)").ToList();

            // if any of them, for any reason, are not equal, something is very wrong
            if (bandwidth.Count != baseUrl.Count || framerate.Count != baseUrl.Count)
                throw new Exception($"Reddit: Something went terribly wrong with {Url}");

            if (baseUrl.Count == 0 || baseUrl == null)
                throw new Exception("Not a video!");

            // make sure our list is actually empty
            videos.Clear();

            // go through each URL, and create a new video object based off it
            for (int i = 0; i < baseUrl.Count; i++)
            {
                // grab the media_url from the dashUrl
                var media_url = dashUrl.Replace("DASHPlaylist.mpd", "");

                // prep the metadata to be put into the video object
                var _url = media_url + baseUrl[i].Value + (hasExtension ? ".mp4" : "");
                int _resolution;

                if(hasExtension)
                    _resolution = int.Parse(Regex.Match(_url, @"(?<=DASH_)(.*)(?=\.mp4)").Value);
                else
                    _resolution = int.Parse(Regex.Match(_url, @"(?<=DASH_)(.*)").Value);

                var _bandwidth = int.Parse(bandwidth[i].Value);
                var _framerate = int.Parse(framerate[i].Value);
                var _audio = media_url + (hasAudio ? "DASH_audio.mp4" : (oldAudio ? "audio" : null));

                // create a new video object, and fill it with metadata 
                var videoObject = new RedditVideo()
                {
                    Url = _url,
                    Bandwidth = _bandwidth,
                    Resolution = _resolution,
                    Framerate = _framerate,
                    AudioUrl = _audio
                };

                // add the new video object to our list
                videos.Add(videoObject);
            }
        }
        int GetBestResolutionIndex()
        {
            var webHandler = new WebHandler();
            int index = -1;
            // loop through every item in the video list
            for (int i = videos.Count; i-- > 0;)
            {
                // if said item is smaller in size than the discord limit, set it's index as the output and break the loop
                if (webHandler.GetContentLength(videos[i].Url) <= limit)
                {
                    index = i;
                    break;
                }
            }
            // return the index
            return index;
        }

        public Stream? DownloadVideoOnly()
        {
            // if we have no video urls, just return null
            if (videos == null)
                return null;

            // select the best resolution that fits within our specifications
            var index = GetBestResolutionIndex();
            // if something went wrong, return null
            if (index == -1)
                return null;
            // if not, continue
            var video = videos[index];
            Video = video;

            // download the stream and return it
            var webhandler = new WebHandler();
            var stream = webhandler.DownloadStream(video.Url);
            return stream;
        }

        public Stream? DownloadAudioOnly()
        {
            // if we have no video urls, just return null
            if (videos == null)
                return null;

            // it is extremely unlikely that an audio file is greater than 8 MiB, so we assume its not
            var video = videos.Last();
            Video = video;

            // if audio track is nonexistant, return null
            if (video.AudioUrl == null)
                return null;

            // download the stream and return it
            var webhandler = new WebHandler();
            var stream = webhandler.DownloadStream(video.AudioUrl);
            return stream;
        }

        public Stream? DownloadMuxxedVideo()
        {
            // if we have no video urls, just return null
            if (videos == null)
                return null;

            // select the best resolution that fits within our specifications
            var index = GetBestResolutionIndex();

            // if something went wrong, return null
            if (index == -1)
                return null;
            // if not, continue
            var video = videos[index];
            Video = video;

            // download the streams
            var webhandler = new WebHandler();
            Stream? videoStream = webhandler.DownloadStream(video.Url);
            Stream? audioStream = webhandler.DownloadStream(video.AudioUrl);

            // if both streams are missing, return null
            if (audioStream == null && videoStream == null)
                return null;
            // if the audio stream is missing, return the video stream
            if (audioStream == null)
                return videoStream;
            // if the video stream is missing, return the audio stream
            if (videoStream == null)
                return audioStream;

            // due to FFMpeg not cooperating, we have to do this the old fashioned way
            // create a new directory if its non existant
            if (!Directory.Exists("temp"))
                Directory.CreateDirectory("temp");

            // create a new string for both names
            var hash = DateTime.Now.GetHashCode();
            var videofile = hash + "_VID.mp4";
            var audiofile = hash + "_AUD.mp4";
            var muxfile = hash + "_MUX.mp4";

            // save the streams to the directory by using a hash as their name
            using (var file = File.Create($"temp/{videofile}"))
                videoStream.CopyTo(file);
            using (var file = File.Create($"temp/{audiofile}"))
                audioStream.CopyTo(file);

            // using the actual files, use FFMpeg to combine them by overlaying the audio over the video
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i temp/{videofile} -i temp/{audiofile} -c:v copy -c:a aac -strict experimental -map 0:v:0 -map 1:a:0 temp/{muxfile}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            // start the process
            process.Start();
            // wait for it to finish
            process.WaitForExit();

            // load the mux file into a Stream object and return it
            Stream? stream = null;

            stream = File.OpenRead($"temp/{muxfile}");
            Filename = muxfile;

            try
            {
                // since we are done, delete all associated files if they exist
                if (File.Exists($"temp/{videofile}"))
                    File.Delete($"temp/{videofile}");
                if (File.Exists($"temp/{audiofile}"))
                    File.Delete($"temp/{audiofile}");
            }
            catch (Exception e)
            {

            }

            // return the muxxed stream
            return stream;
        }
    }
}
