using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DownloadBot.Services
{
    public struct Video
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public int Bandwidth { get; set; }
        public int Resolution { get; set; }
        public int Framerate { get; set; }
        public string? AudioUrl { get; set; }

        public override string ToString()
            => $"Video: {Title}, {Url}, {Bandwidth} bits, {Resolution}p, {Framerate} fps, {AudioUrl}";
    }

    public class Reddit
    {
        string url = "";
        int limit = 8000000;

        List<Video> videos = new List<Video>();

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

        public string Title { get; private set; }

        public void GetVideoURL()
        {
            // create a webhandler object, and grab the json file
            var webhandler = new WebHandler();
            // remove anything after a question mark
            var cleanUrl = Regex.Replace(Url, @"\?(.*)", "");
            var json = webhandler.DownloadString(cleanUrl + ".json?limit=1");

            // if the json is empty for some reason, throw an exception
            if (string.IsNullOrEmpty(json))
                throw new Exception("Empty JSON");

            // verify that the MPD exists
            if (!json.Contains("mpd"))
                throw new Exception("No video found!");
            
            // grab the mpd url using regex
            var mpd_url = Regex.Replace(json, @".*(?<=\""dash_url\"": \"")(.*)(?=\"", \""duration\"":).*$", @"$1");
            mpd_url = Regex.Replace(mpd_url, @"\?(.*)", "");
            if (string.IsNullOrEmpty(mpd_url))
                throw new Exception("No video found");

            // grab the post title from the JSON while we have it
            var title = Regex.Replace(json, @".*(?<=\""title\"": \"")(.*)(?=\"", \""link_flair_richtext\"":).*$", @"$1");

            // grab video url, which we need to reconstruct the mpd urls
            string media_url = Regex.Replace(json,
                @".*(?<=\""url_overridden_by_dest\"": \"")(.*)(?=\"", \""view_count\"":).*$", @"$1");

            // download the mpd contents
            var mpd = webhandler.DownloadString(mpd_url);
            // check if video contains audio
            bool hasAudio = mpd.Contains("DASH_audio.mp4");
            
            // grab metadata using regex
            var bandwidth = Regex.Matches(mpd, @"(?<=Representation bandwidth="")(.*)(?="" c)").ToList();
            var baseUrl = Regex.Matches(mpd, @"(?<=BaseURL>)((DASH_220.mp4|DASH_240.mp4|DASH_360.mp4|DASH_480.mp4|DASH_720.mp4|DASH_1080.mp4)*)(?=<)").ToList();
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
                // prep the metadata to be put into the video object
                var _url = media_url + "/" + baseUrl[i].Value;
                var _bandwidth = int.Parse(bandwidth[i].Value);
                var _resolution = int.Parse(Regex.Match(_url, @"(?<=DASH_)(.*)(?=\.mp4)").Value);
                var _framerate = int.Parse(framerate[i].Value);
                var _audio = hasAudio ? media_url + "/DASH_audio.mp4" : null;

                // create a new video object, and fill it with metadata 
                var videoObject = new Video()
                {
                    Title = title,
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

            // set the video title
            Title = video.Title + "_" + video.Resolution + "p";

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

            // set the video title
            Title = video.Title;

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

            // set the video title
            Title = video.Title + "_" + video.Resolution + "p";

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
            catch(Exception e)
            {
                
            }
            
            // return the muxxed stream
            return stream;
        }
    }
}
