﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
﻿using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Serialization;

namespace MediaBrowser.Channels.JupiterBroadcasting
{
	public class JupiterBroadcastingChannel : IChannel, IRequiresMediaInfoCallback, ISupportsLatestMedia
	{
		private readonly IHttpClient _httpClient;
		private readonly ILogger _logger;
		private readonly IXmlSerializer _xmlSerializer;

		public JupiterBroadcastingChannel(IHttpClient httpClient, IXmlSerializer xmlSerializer, ILogManager logManager)
		{
			_httpClient = httpClient;
			_logger = logManager.GetLogger(GetType().Name);
			_xmlSerializer = xmlSerializer;
		}

		// Implementing the IChannel interface.

		public string Name
		{
			get { return "Jupiter Broadcasting"; }
		}

		public string Description
		{
			get { return string.Empty; }
		}

		public string DataVersion
		{
			// Increment as needed to invalidate all caches.
			get { return "1"; }
		}

		public string HomePageUrl
		{
			get { return "http://www.jupiterbroadcasting.com"; }
		}

		public ChannelParentalRating ParentalRating 
		{
			get { return ChannelParentalRating.GeneralAudience; }
		}

		//TODO: Add the image collection and change the image type to whatever I have.
		public Task<DynamicImageResponse> GetChannelImage (ImageType type, CancellationToken cancellationToken)
		{
			switch (type)
			{
			case ImageType.Thumb:
                {
                    var path = GetType().Namespace + ".Resources.images.thumb.png";

                    return Task.FromResult(new DynamicImageResponse
                    {
                        Format = ImageFormat.Png,
                        HasImage = true,
                        Stream = GetType().Assembly.GetManifestResourceStream(path)
                    });
                }
			case ImageType.Backdrop:
			case ImageType.Primary:
				{
					var path = GetType().Namespace + ".Resources.images.jupiterbroadcasting.png";

					return Task.FromResult(new DynamicImageResponse
						{
							Format = ImageFormat.Png,
							HasImage = true,
							Stream = GetType().Assembly.GetManifestResourceStream(path)
						});
				}
			default:
				throw new ArgumentException("Unsupported image type: " + type);
			}
		}

		public InternalChannelFeatures GetChannelFeatures ()
		{
			return new InternalChannelFeatures
			{
				ContentTypes = new List<ChannelMediaContentType>
				{
					ChannelMediaContentType.Podcast
				},

				MediaTypes = new List<ChannelMediaType>
				{
					ChannelMediaType.Video
				},

				MaxPageSize = 100,

				DefaultSortFields = new List<ChannelItemSortField>
				{
					ChannelItemSortField.Name,
					ChannelItemSortField.PremiereDate,
					ChannelItemSortField.Runtime,
				},
			};
		}

		private async Task<ChannelItemResult> GetChannelsInternal(InternalChannelItemQuery query, CancellationToken cancellationToken)
		{
			_logger.Debug ("Channels ID: " + query.FolderId);
			var jupiterChannels = new List<ChannelItemInfo>();

			var masterChannelList = new List<KeyValuePair<string,string>> {
				new KeyValuePair<string, string>("faux", "FauxShow"),
				new KeyValuePair<string, string>("scibyte", "SciByte"),
				new KeyValuePair<string, string>("unfilter", "Unfilter"),
				new KeyValuePair<string, string>("techsnap","TechSNAP"),
				new KeyValuePair<string, string>("howto", "HowTo Linux"),
				new KeyValuePair<string, string>("bsd", "BSD Now"),
				new KeyValuePair<string, string>("las", "Linux Action Show"),
                new KeyValuePair<string, string>("coder", "Coder Radio"),
                new KeyValuePair<string, string>("unplugged", "Linux Unplugged"),
                new KeyValuePair<string, string>("techtalk", "Tech Talk Today")
			};

			foreach (var currentChannel in masterChannelList)
			{
				jupiterChannels.Add (new ChannelItemInfo 
				{
					Type = ChannelItemType.Folder,
					ImageUrl = "https://raw.githubusercontent.com/DaBungalow/MediaBrowser.Channels.JupiterBroadcasting/master/Resources/images/" + currentChannel.Key + ".jpg",
					Name = currentChannel.Value,
					Id = currentChannel.Key
				});
			}

			return new ChannelItemResult
			{
				Items = jupiterChannels.ToList (),
				TotalRecordCount = masterChannelList.Count
			};
		}

		private async Task<ChannelItemResult> GetChannelItemsInternal(InternalChannelItemQuery query, CancellationToken cancellationToken)
		{
            _logger.Debug("Getting internal channel items: " + query.FolderId);
			var offset = query.StartIndex.GetValueOrDefault();
			var downloader = new JupiterChannelItemsDownloader(_logger, _xmlSerializer, _httpClient);

			string baseurl;

			switch (query.FolderId)
			{
			case "faux":
				baseurl = "http://www.jupiterbroadcasting.com/feeds/FauxShowHD.xml";
				break;
			case "scibyte":
				baseurl = "http://feeds.feedburner.com/scibytehd";
				break;
			case "unfilter":
				baseurl = "http://www.jupiterbroadcasting.com/feeds/unfilterHD.xml";
				break;
			case "techsnap":
				baseurl = "http://feeds.feedburner.com/techsnaphd";
				break;
			case "howto":
				baseurl = "http://feeds.feedburner.com/HowtoLinuxHd";
				break;
			case "bsd":
				baseurl = "http://feeds.feedburner.com/BsdNowHd";
				break;
			case "las":
				baseurl = "http://feeds.feedburner.com/linuxashd";
				break;
            case "unplugged":
                baseurl = "http://feeds.feedburner.com/linuxunvid";
                break;
            case "coder":
                baseurl = "http://feeds.feedburner.com/coderradiovideo";
                break;
            case "techtalk":
                baseurl = "http://feedpress.me/t3mob";
                break;
			default:
				throw new ArgumentException("FolderId was not what I expected: " + query.FolderId);
			}

			var podcasts = await downloader.GetStreamList(baseurl, offset, cancellationToken).ConfigureAwait(false);

			var itemslist = podcasts.channel.item;

			var items = new List<ChannelItemInfo>();

			foreach (var podcast in podcasts.channel.item)
			{
				var mediaInfo = new List<ChannelMediaInfo>{};
                if(query.FolderId == "coder" || query.FolderId == "unplugged" || query.FolderId == "techtalk")
				{
                    mediaInfo.Add(new ChannelMediaInfo
                    {
                        Protocol = MediaProtocol.Http,
                        Path = podcast.enclosure.url,
                        Width = 768,
                        Height = 432
                    });
				}
                else
                {
                    mediaInfo.Add(new ChannelMediaInfo
                    {
                        Protocol = MediaProtocol.Http,
                        Path = podcast.enclosure.url,
                        Width = 1200,
                        Height = 720
                    });
                }
                //For some unknown reason, attempting to parse the runtime throws a null object reference exception.

                long runtime;

                if (podcast.duration != null)
                {
                    var runtimeArray = podcast.duration.Split(':');
                    int hours;
                    int minutes;
                    int seconds;
                    if (runtimeArray.Length == 3)
                    {                     
                        int.TryParse(runtimeArray[0], out hours);
                        int.TryParse(runtimeArray[1], out minutes);
                        int.TryParse(runtimeArray[2], out seconds);
                    }
                    else
                    {
                        hours = 0;
                        int.TryParse(runtimeArray[0], out minutes);
                        int.TryParse(runtimeArray[1], out seconds);
                    }
                    runtime = (hours * 3600) + (minutes * 60) + seconds;
                    runtime = TimeSpan.FromSeconds(runtime).Ticks;

                    items.Add(new ChannelItemInfo
                    {
                        ContentType = ChannelMediaContentType.Podcast,
                        ImageUrl = "https://raw.githubusercontent.com/DaBungalow/MediaBrowser.Channels.JupiterBroadcasting/master/Resources/images/" + query.FolderId + ".jpg",
                        IsInfiniteStream = true,
                        MediaType = ChannelMediaType.Video,
                        MediaSources = mediaInfo,
                        RunTimeTicks = runtime,
                        Name = podcast.title,
                        Id = podcast.enclosure.url,
                        Type = ChannelItemType.Media,
                        DateCreated = !String.IsNullOrEmpty(podcast.pubDate) ?
                            Convert.ToDateTime(podcast.pubDate) : (DateTime?)null,
                        PremiereDate = !String.IsNullOrEmpty(podcast.pubDate) ?
                            Convert.ToDateTime(podcast.pubDate) : (DateTime?)null,
                        Overview = podcast.summary,
                    });
                }
                else
                {
                    items.Add(new ChannelItemInfo
                    {
                        ContentType = ChannelMediaContentType.Podcast,
                        ImageUrl = "https://raw.githubusercontent.com/DaBungalow/MediaBrowser.Channels.JupiterBroadcasting/master/Resources/images/" + query.FolderId + ".jpg",
                        IsInfiniteStream = true,
                        MediaType = ChannelMediaType.Video,
                        MediaSources = mediaInfo,
                        Name = podcast.title,
                        Id = podcast.enclosure.url,
                        Type = ChannelItemType.Media,
                        DateCreated = !String.IsNullOrEmpty(podcast.pubDate) ?
                            Convert.ToDateTime(podcast.pubDate) : (DateTime?)null,
                        PremiereDate = !String.IsNullOrEmpty(podcast.pubDate) ?
                            Convert.ToDateTime(podcast.pubDate) : (DateTime?)null,
                        Overview = podcast.summary,
                    });
                }
			}

			return new ChannelItemResult
			{
				Items = items,
				TotalRecordCount = items.Count,
			};
		}

		public async Task<ChannelItemResult> GetChannelItems (InternalChannelItemQuery query, CancellationToken cancellationToken)
		{
			ChannelItemResult result;

			if (query.FolderId == null) {
				result = await GetChannelsInternal(query, cancellationToken).ConfigureAwait(false);
			} 
			else 
			{
				result = await GetChannelItemsInternal(query, cancellationToken).ConfigureAwait(false);
			}

			return result;
		}

		public IEnumerable<ImageType> GetSupportedChannelImages ()
		{
			return new List<ImageType>
			{
				ImageType.Thumb,
				ImageType.Primary,
				ImageType.Backdrop
			};
		}

		public bool IsEnabledFor (string userId)
		{
			return true;
		}

	    // IRequiresMediaInfoCallback implementation

		public async Task<IEnumerable<ChannelMediaInfo>> GetChannelItemMediaInfo (string id, CancellationToken cancellationToken)
		{
			return new List<ChannelMediaInfo>
			{
				new ChannelMediaInfo
				{
					Path = id,
					Width = 1280,
					Height = 720,
				}
			};
		}

        private async Task<IEnumerable<ChannelItemInfo>> GetChannelItemsInternal(string feedUrl, CancellationToken cancellationToken)
        {
            int offset = 0;

		    var downloader = new JupiterChannelItemsDownloader(_logger, _xmlSerializer, _httpClient);

			string baseurl = feedUrl;
            string folderid;

            switch (baseurl)
            {
                case "http://www.jupiterbroadcasting.com/feeds/FauxShowHD.xml":
                    folderid = "faux";
                    break;
                case "http://feeds.feedburner.com/scibytehd":
                    folderid = "scibyte";
                    break;
                case "http://www.jupiterbroadcasting.com/feeds/unfilterHD.xml":
                    folderid = "unfilter";
                    break;
                case "http://feeds.feedburner.com/techsnaphd":
                    folderid = "techsnap";
                    break;
                case "http://feeds.feedburner.com/HowtoLinuxHd":
                    folderid = "howto";
                    break;
                case "http://feeds.feedburner.com/BsdNowHd":
                    folderid = "bsd";
                    break;
                case "http://feeds.feedburner.com/linuxashd":
                    folderid = "las";
                    break;
                case "http://feeds.feedburner.com/linuxunvid":
                    folderid = "unplugged";
                    break;
                case "http://feeds.feedburner.com/coderradiovideo":
                    folderid = "coder";
                    break;
                case "http://feedpress.me/t3mob":
                    folderid = "techtalk";
                    break;
                default:
                    folderid = "jupiterbroadcasting";
                    break;
            }

			var podcasts = await downloader.GetStreamList(baseurl, offset, cancellationToken).ConfigureAwait(false);

			var itemslist = podcasts.channel.item;

			var items = new List<ChannelItemInfo>();

			foreach (var podcast in podcasts.channel.item)
			{
				var mediaInfo = new List<ChannelMediaInfo>{};
                if (baseurl == "http://feeds.feedburner.com/coderradiovideo" ||
                    baseurl == "http://feeds.feedburner.com/linuxunvid" ||
                    baseurl == "http://feedpress.me/t3mob")
				{
                    mediaInfo.Add(new ChannelMediaInfo
                    {
                        Protocol = MediaProtocol.Http,
                        Path = podcast.enclosure.url,
                        Width = 768,
                        Height = 432
                    });
				}
                else
                {
                    mediaInfo.Add(new ChannelMediaInfo
                    {
                        Protocol = MediaProtocol.Http,
                        Path = podcast.enclosure.url,
                        Width = 1200,
                        Height = 720
                    });
                }

                long runtime;

                if (podcast.duration != null)
                {
                    var runtimeArray = podcast.duration.Split(':');
                    int hours;
                    int minutes;
                    int seconds;
                    if (runtimeArray.Length == 3)
                    {
                        int.TryParse(runtimeArray[0], out hours);
                        int.TryParse(runtimeArray[1], out minutes);
                        int.TryParse(runtimeArray[2], out seconds);
                    }
                    else
                    {
                        hours = 0;
                        int.TryParse(runtimeArray[0], out minutes);
                        int.TryParse(runtimeArray[1], out seconds);
                    }
                    runtime = (hours * 3600) + (minutes * 60) + seconds;
                    runtime = TimeSpan.FromSeconds(runtime).Ticks;

                    items.Add(new ChannelItemInfo
                    {
                        ContentType = ChannelMediaContentType.Podcast,
                        ImageUrl = "https://raw.githubusercontent.com/DaBungalow/MediaBrowser.Channels.JupiterBroadcasting/master/Resources/images/" + folderid + ".jpg",
                        IsInfiniteStream = true,
                        MediaType = ChannelMediaType.Video,
                        MediaSources = mediaInfo,
                        RunTimeTicks = runtime,
                        Name = podcast.title,
                        Id = podcast.enclosure.url,
                        Type = ChannelItemType.Media,
                        DateCreated = !String.IsNullOrEmpty(podcast.pubDate) ?
                            Convert.ToDateTime(podcast.pubDate) : (DateTime?)null,
                        PremiereDate = !String.IsNullOrEmpty(podcast.pubDate) ?
                            Convert.ToDateTime(podcast.pubDate) : (DateTime?)null,
                        Overview = podcast.summary,
                    });
                }
                else
                {
                    items.Add(new ChannelItemInfo
                    {
                        ContentType = ChannelMediaContentType.Podcast,
                        ImageUrl = "https://raw.githubusercontent.com/DaBungalow/MediaBrowser.Channels.JupiterBroadcasting/master/Resources/images/" + folderid + ".jpg",
                        IsInfiniteStream = true,
                        MediaType = ChannelMediaType.Video,
                        MediaSources = mediaInfo,
                        Name = podcast.title,
                        Id = podcast.enclosure.url,
                        Type = ChannelItemType.Media,
                        DateCreated = !String.IsNullOrEmpty(podcast.pubDate) ?
                            Convert.ToDateTime(podcast.pubDate) : (DateTime?)null,
                        PremiereDate = !String.IsNullOrEmpty(podcast.pubDate) ?
                            Convert.ToDateTime(podcast.pubDate) : (DateTime?)null,
                        Overview = podcast.summary,
                    });
                }
            }
            return items;
        }

        private async Task<ChannelItemResult> GetAllMedia(InternalAllChannelMediaQuery query, CancellationToken cancellationToken)
        {
            if (query.ContentTypes.Length > 0 && !query.ContentTypes.Contains(ChannelMediaContentType.Podcast))
            {
                return new ChannelItemResult();
            }

            string[] urls = {
                "http://www.jupiterbroadcasting.com/feeds/FauxShowHD.xml",
                "http://feeds.feedburner.com/scibytehd",
                "http://www.jupiterbroadcasting.com/feeds/unfilterHD.xml",
                "http://feeds.feedburner.com/techsnaphd",
                "http://feeds.feedburner.com/HowtoLinuxHd",
                "http://feeds.feedburner.com/BsdNowHd",
                "http://feeds.feedburner.com/linuxashd",
                "http://feeds.feedburner.com/linuxunvid",
                "http://feeds.feedburner.com/coderradiovideo",
                "http://feedpress.me/t3mob"
            };

            var tasks = urls.Select(async i =>
            {
                try
                {
                    return await GetChannelItemsInternal(i, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Info("Failed to fetch the latest episodes: " + ex);
                    return new List<ChannelItemInfo>();
                }
            });
            
            var items = (await Task.WhenAll(tasks).ConfigureAwait(false))
                .SelectMany(i => i);

            if (query.ContentTypes.Length > 0)
            {
                items = items.Where(i => query.ContentTypes.Contains(i.ContentType));
            }

            var all = items.ToList();

            return new ChannelItemResult
            {
                Items = all,
                TotalRecordCount = all.Count
            };
        }

        public async Task<IEnumerable<ChannelItemInfo>> GetLatestMedia(ChannelLatestMediaSearch request, CancellationToken cancellationToken)
        {
            // Looks like the only way we can do this is by getting all, then sorting

            var all = await GetAllMedia(new InternalAllChannelMediaQuery
            {
                UserId = request.UserId

            }, cancellationToken).ConfigureAwait(false);

            return all.Items.OrderByDescending(i => i.DateCreated ?? DateTime.MinValue);
        }
	}
}

