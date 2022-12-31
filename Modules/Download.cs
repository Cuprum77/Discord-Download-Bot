using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode;
using DownloadBot.Services;
using DownloadBot.Enums;
using YoutubeExplode.Videos;

namespace DownloadBot.Modules
{
    public class Download : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly WebHandler webhandler;

        public Download(WebHandler webhandler)
        {
            this.webhandler = webhandler;
        }

        [SlashCommand("ytd", "Download a youtube video", runMode: RunMode.Async)]
        public async Task YoutubeDownloadAsync(string url, MediaType type = MediaType.Video, YesNo statistics = YesNo.Yes)
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            // defer the interraction to buy us some time
            await DeferAsync();

            // originally i wanted it to be able to search through previous messages, but this is rather buggy and tedious for minimal gain
            // check if the string isnt empty, as a sanity check
            if (string.IsNullOrEmpty(url))
            {
                await FollowupAsync("Not a valid Youtube URL");
                return;
            }

            // make sure the user provided an actual video URL using regex, luckily youtube only supports HTTPS
            if (!Regex.Match(url, @"(https?:\/\/|)(www\.|)?(youtube.com/(shorts/|watch\?v?)|youtu.be/)").Success)
            {
                // try and fix the URL in case the user only provided the video id
                url = "https://youtu.be/" + url;

                // if it still fails, throw an error
                if (!webhandler.isYoutube(url))
                {
                    await FollowupAsync("Not a valid Youtube URL");
                    return;
                }
            }

            // check if user forgot to add https:// prefix
            if (!Regex.Match(url, @"https?:\/\/").Success)
                url = "https://" + url;

            // if we somehow messed up and its no longer an URL, fail
            if (!webhandler.isURI(url) || !webhandler.isYoutube(url))
            {
                await FollowupAsync("Not a valid Youtube URL");
                return;
            }

            // create a new youtube object
            var youtube = new YoutubeClient();
            // get the video
            StreamManifest? streamManifest;
            Video? video;
            try
            {
                streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
                video = await youtube.Videos.GetAsync(url);
            }
            catch(Exception e)
            {
                await FollowupAsync("Failed to download video");
                return;
            }

            // get video metadata
            var title = video.Title;

            // store the video and extension here
            IEnumerable<IStreamInfo> streamInfo;
            string extension;

            // change format depending on selection
            switch (type)
            {
                case MediaType.Video:
                    streamInfo = streamManifest.GetMuxedStreams();
                    extension = ".webm";
                    break;
                case MediaType.AudioOnly:
                    streamInfo = streamManifest.GetAudioOnlyStreams();
                    extension = ".webm";
                    break;
                case MediaType.VideoOnly:
                    streamInfo = streamManifest.GetVideoOnlyStreams();
                    extension = ".webm";
                    break;
                default:
                    return;
            }

            // attempt to find a resolution that fits within discords 8 MB limit
            streamInfo = streamInfo.OrderByDescending(o => o.Size.KiloBytes);
            IStreamInfo? videoInfo = null;

            // grab the largest file possible, while maintaining discords limit
            foreach (var _stream in streamInfo)
            {
                videoInfo = _stream;
                if (_stream.Size.Bytes < Bot.MaxFileSize)
                    break;
            }

            // warn if file is too big or if the video is missing for some reason
            if (videoInfo == null || videoInfo.Size.MegaBytes > 8.388608)
            {
                await FollowupAsync($"File is too big, Discord limits it to 8 MiB (~8.3 MB), " +
                    $"yours is {Math.Round(videoInfo.Size.MegaBytes, 2)} MB", ephemeral: true);
                return;
            }

            // add the extension onto the title
            title = Regex.Replace(title, "[^0-9a-zA-Z]+", "");
            title += extension;

            // get the stream
            var stream = await youtube.Videos.Streams.GetAsync(videoInfo);

            // create an embed for it
            var stats = new Statistics();
            var views = stats.YoutubeTruncate(video.Engagement.ViewCount);
            var likes = stats.YoutubeTruncate(video.Engagement.LikeCount);

            var fieldEmbed = new EmbedFieldBuilder()
            {
                Name = "Statistics",
                Value = $"""
                :smiley: Creator [{video.Author.ChannelTitle}]({video.Author.ChannelUrl})
                :date: Uploaded <t:{video.UploadDate.ToUnixTimeSeconds()}:R>
                :eyes: Views {views}
                :thumbsup: Likes  {likes}
                """
            };

            var authorEmbed = new EmbedAuthorBuilder()
            {
                Name = Context.User.Username + "#" + Context.User.Discriminator,
                IconUrl = Context.User.GetAvatarUrl()
            };

            var embed = new EmbedBuilder()
                 .WithAuthor(authorEmbed)
                 .WithTitle(video.Title)
                 .WithUrl(url)
                 .WithImageUrl(video.Thumbnails[2].Url)
                 .WithFooter(DateTime.UtcNow.ToString() + " UTC")
                 .WithFields(fieldEmbed)
                 .Build();

            // send the stream as a response to the user
            Embed? actualEmbed = statistics == YesNo.No ? null : embed;
            await FollowupWithFileAsync(stream, title, embed: embed);
            watch.Stop();
            Console.WriteLine($"/ytd took me {watch.ElapsedMilliseconds} ms to run");
        }


        [SlashCommand("rtd", "Download a reddit video", runMode: RunMode.Async)]
        public async Task RedditDownloadAsync(string url, MediaType type = MediaType.Video, YesNo statistics = YesNo.Yes)
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            // defer the interraction to buy us some time
            await DeferAsync();

            // if we somehow messed up and its no longer an URL, fail
            if (!webhandler.isURI(url) || string.IsNullOrEmpty(url))
            {
                await FollowupAsync("Not a valid Reddit URL");
                return;
            }

            // create a new Reddit object
            var reddit = new Reddit(url);

            try
            {
                reddit.DownloadMetaData();
                reddit.DownloadVideoData();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await FollowupAsync("Failed to download video");
                return;
            }

            // if its marked as 18+, we need to take care not to post it in a non 18+ channel
            if(Context.Channel is ITextChannel)
            {
                if (!(Context.Channel as ITextChannel).IsNsfw && reddit.Post.AgeRestricted != null)
                {
                    if ((bool)reddit.Post.AgeRestricted)
                    {
                        await FollowupAsync("You cant post adult content in a SFW channel!");
                        return;
                    }
                }
            }

            Stream? stream;
            string title;
            string filename;

            // change format depending on selection
            switch (type)
            {
                case MediaType.Video:
                    stream = reddit.DownloadMuxxedVideo();
                    title = Regex.Replace(reddit.Post.Title, "[^0-9a-zA-Z]+", "");
                    filename = title + "_" + reddit.Video.Resolution + "p" + ".webm";
                    break;
                case MediaType.AudioOnly:
                    stream = reddit.DownloadAudioOnly();
                    title = Regex.Replace(reddit.Post.Title, "[^0-9a-zA-Z]+", "");
                    filename = title + ".mp3";
                    break;
                case MediaType.VideoOnly:
                    stream = reddit.DownloadVideoOnly();
                    title = Regex.Replace(reddit.Post.Title, "[^0-9a-zA-Z]+", "");
                    filename = title + "_" + reddit.Video.Resolution + "p" + ".webm";
                    break;
                default:
                    await FollowupAsync("Something went wrong");
                    return;
            }

            // if the stream is null, the video is missing
            if (stream == null)
            {
                await FollowupAsync("Failed to download video");
                return;
            }

            // create an embed for it
            var stats = new Statistics();
            var likes = stats.YoutubeTruncate((double)(reddit.Post.Upvotes ?? 0));
            var coinsTotal = stats.YoutubeTruncate((double)(reddit.Awards.TotalCount()));
            var coinsCost = stats.YoutubeTruncate((double)(reddit.Awards.TotalCost()));
            var dayspremium = stats.YoutubeTruncate((double)(reddit.Awards.TotalDaysPremium()));

            // calculate the coins per dollar price
            var price = 500 / 1.99;
            // if we now divide the total coins by the coins per dollar, we should get the price in dollars
            var coinCostDollars = Math.Round((double)reddit.Awards.TotalCost() / price, 2);

            var postField = new EmbedFieldBuilder()
            {
                Name = "Post Statistics",
                Value = $"""
                :smiley: User [{reddit.User.Username}]({reddit.User.Url}/)
                :date: Uploaded <t:{reddit.Post.CreatedAtUTC}:R>
                :thumbsup: Upvotes {likes}
                :military_medal: Awards {coinsTotal}
                :coin: Award Cost {coinsCost}
                :moneybag: Theoretical Cost* {coinCostDollars} $
                :tickets: Days Premium Gained {dayspremium}

                * Does not take into account free awards nor bundles
                Note: Older posts may not download correctly!
                """
            };

            var authorEmbed = new EmbedAuthorBuilder()
            {
                Name = reddit.Subreddit.Name,
                Url = reddit.Subreddit.Url,
                IconUrl = reddit.Subreddit.IconUrl
            };

            var embed = new EmbedBuilder()
                 .WithAuthor(authorEmbed)
                 .WithTitle(reddit.Post.Title)
                 .WithUrl(url)
                 .WithImageUrl(reddit.Image.Last().Url)
                 .WithFooter(DateTime.UtcNow.ToString() + " UTC")
                 .WithFields(postField)
                 .Build();

            // send the stream as a response to the user
            Embed? actualEmbed = statistics == YesNo.No ? null : embed;
            // send the stream as a response to the user
            await FollowupWithFileAsync(stream, filename, embed: actualEmbed);

            if (type == MediaType.Video)
                try { File.Delete($"temp/{reddit.Filename}"); } catch { }
            watch.Stop();
            Console.WriteLine($"/rtd took me {watch.ElapsedMilliseconds} ms to run");
        }
    }
}
