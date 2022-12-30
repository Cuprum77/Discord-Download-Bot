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
        public async Task YoutubeDownloadAsync(string? url = null, MediaType type = MediaType.Video, YesNo statistics = YesNo.Yes)
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            // defer the interraction to buy us some time
            await DeferAsync();

            // attempt to find the url if its not present
            if (string.IsNullOrEmpty(url))
            {
                // get the last n messages in the channel
                var messages = await Context.Channel.GetMessagesAsync(Bot.SearchLimit).FlattenAsync();

                // loop through each message and check if its a real URI
                foreach (var message in messages)
                {
                    // skip empty messages
                    if (string.IsNullOrEmpty(message.Content))
                        continue;

                    // if theres an embed, it contain a youtube url
                    if (message.Embeds.Count > 0)
                    {
                        // check the embeds if it contains any youtube links
                        url = webhandler.CheckEmbeds(message);
                        // if there was a link, break the loop
                        if (!string.IsNullOrEmpty(url))
                            break;
                    }

                    // check if the message itself contains a url thats not embedded
                    url = message.Content;
                    if (webhandler.isURI(url))
                    {
                        // check if the url is a youtube url
                        if (webhandler.isYoutube(url))
                            break;
                    }
                }
            }
            else
            {
                // make sure the user provided an actual video URL using regex, luckily youtube only supports HTTPS
                if (!Regex.Match(url, @"(https?:\/\/|)(www\.|)?(youtube.com/(shorts/|watch\?v?)|youtu.be/)").Success)
                {
                    // try and fix the URL in case the user only provided the video id
                    url = "https://youtu.be/" + url;

                    // if it still fails, throw an error
                    if (!webhandler.isYoutube(url))
                    {
                        await FollowupAsync("Not a valid Youtube URL", ephemeral: true);
                        return;
                    }
                }

                // check if user forgot to add https:// prefix
                if (!Regex.Match(url, @"https?:\/\/").Success)
                    url = "https://" + url;

                // if we somehow messed up and its no longer an URL, fail
                if (!webhandler.isURI(url) || !webhandler.isYoutube(url))
                {
                    await FollowupAsync("Not a valid Youtube URL", ephemeral: true);
                    return;
                }
            }

            // create a new youtube object
            var youtube = new YoutubeClient();
            // get the video
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
            var video = await youtube.Videos.GetAsync(url);

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
            title += extension;

            // get the stream
            var stream = await youtube.Videos.Streams.GetAsync(videoInfo);

            // create an embed for it
            var fieldEmbed = new EmbedFieldBuilder()
            {
                Name = "Statistics",
                Value = $"""
                :eyes: Views `{video.Engagement.ViewCount}`
                :thumbsup: Likes  `{video.Engagement.LikeCount}`
                :date: Uploaded <t:{video.UploadDate.ToUnixTimeSeconds()}:R>
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
        public async Task RedditDownloadAsync(string url, MediaType type = MediaType.Video)
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            // defer the interraction to buy us some time
            await DeferAsync();

            // if we somehow messed up and its no longer an URL, fail
            if (!webhandler.isURI(url) || string.IsNullOrEmpty(url))
            {
                await FollowupAsync("Not a valid Reddit URL", ephemeral: true);
                return;
            }

            // create a new Reddit object
            var reddit = new Reddit(url);
            // because reddit is a bit annoying, we wrap it in a try catch
            try 
            { 
                reddit.GetVideoURL(); 
            } catch (Exception ex)
            {
                await FollowupAsync(ex.Message, ephemeral: true);
                return;
            }

            Stream? stream;
            string filename;

            // change format depending on selection
            switch (type)
            {
                case MediaType.Video:
                    stream = reddit.DownloadMuxxedVideo();
                    filename = reddit.Title + ".webm";
                    break;
                case MediaType.AudioOnly:
                    stream = reddit.DownloadAudioOnly();
                    filename = reddit.Title + ".mp3"; 
                    break;
                case MediaType.VideoOnly:
                    stream = reddit.DownloadVideoOnly();
                    filename = reddit.Title + ".webm"; 
                    break;
                default:
                    await FollowupAsync("Something went wrong", ephemeral: true);
                    return;
            }

            // if the stream is null, the video is missing
            if (stream == null)
            {
                await FollowupAsync("Failed to download video", ephemeral: true);
                return;
            }

            // send the stream as a response to the user
            await FollowupWithFileAsync(stream, filename);

            if(type == MediaType.Video)
                try { File.Delete($"temp/{reddit.Filename}"); } catch { }
            watch.Stop();
            Console.WriteLine($"/rtd took me {watch.ElapsedMilliseconds} ms to run");
        }
    }
}
