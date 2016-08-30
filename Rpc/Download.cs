using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokemonGo.RocketAPI.Helpers;
using POGOProtos.Enums;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Requests.Messages;
using POGOProtos.Networking.Responses;

namespace PokemonGo.RocketAPI.Rpc
{
    public class Download : BaseRpc
    {
        public Download(Client client) : base(client)
        {
        }
        public async Task<DownloadSettingsResponse> GetSettings()
        {
            var message = new DownloadSettingsMessage
            {
                Hash = "2788184af4004004d6ab0740f7632983332106f6"
            };
            
            return await PostProtoPayload<Request, DownloadSettingsResponse>(RequestType.DownloadSettings, message);
        }

        public async Task<DownloadItemTemplatesResponse> GetItemTemplates()
        {
            return await PostProtoPayload<Request, DownloadItemTemplatesResponse>(RequestType.DownloadItemTemplates, new DownloadItemTemplatesMessage());
        }

        public async Task<DownloadRemoteConfigVersionResponse> GetRemoteConfigVersion(Platform platform, uint appVersion, string deviceManufacturer, string deviceModel, string locale)
        {
            return await PostProtoPayload<Request, DownloadRemoteConfigVersionResponse>(RequestType.DownloadRemoteConfigVersion, new DownloadRemoteConfigVersionMessage()
            {
                Platform = platform,
                DeviceManufacturer = deviceManufacturer,
                DeviceModel = deviceModel,
                Locale = locale,
                AppVersion = appVersion
            });
        }

        public async Task<GetAssetDigestResponse> GetAssetDigest(uint appVersion, string deviceManufacturer, string deviceModel, string locale, Platform platform)
        {
            return await PostProtoPayload<Request, GetAssetDigestResponse>(RequestType.GetAssetDigest, new GetAssetDigestMessage()
            {
                Platform = platform,
                DeviceManufacturer = deviceManufacturer,
                DeviceModel = deviceModel,
                Locale = locale,
                AppVersion = appVersion
            });
        }

        public async Task<GetDownloadUrlsResponse> GetDownloadUrls(IEnumerable<string> assetIds)
        {
            return await PostProtoPayload<Request, GetDownloadUrlsResponse>(RequestType.GetDownloadUrls, new GetDownloadUrlsMessage()
            {
                AssetId = { assetIds }
            });
        }

    }
}
