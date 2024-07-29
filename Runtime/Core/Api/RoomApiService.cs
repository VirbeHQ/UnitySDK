using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Plugins.Virbe.Core.Api
{
    public sealed class RoomApiService
    {
        private readonly string endpoint;
        private readonly Dictionary<string, string> headers;
        private string locationId;
        private string endUserId;
        private DateTime lastRoomModifiedAt;
        private DateTime lastPollingMessageInstant;
        private string roomId;

        public RoomApiService(string roomUrl, string roomApiAccessKey, string locationId, string endUserId)
        {
            this.endpoint = roomUrl;
            this.locationId = locationId;
            this.endUserId = endUserId;
            this.headers = new Dictionary<string, string>
            {
                { "X-Room-Api-Access-Key", roomApiAccessKey }
            };
            this.lastPollingMessageInstant = DateTime.Now;
        }

        public async Task<T> Get<T>(string path)
        {
            return await Request<T>($"{this.endpoint}{path}", "GET", this.headers);
        }

        public async Task<T> Post<T>(string path, object body)
        {
            return await Request<T>($"{this.endpoint}{path}", "POST", this.headers, body);
        }

        public async Task<RoomDto.Room> CreateRoom()
        {
            var requestBody = new
            {
                locationId = this.locationId,
                endUserId = this.endUserId
            };

            var room = await Post<RoomDto.Room>("", requestBody);
            this.lastPollingMessageInstant = ParseServerTimeString(room.createdAt);
            this.lastRoomModifiedAt = ParseServerTimeString(room.modifiedAt);
            this.roomId = room.id;
            return room;
        }

        public async Task<RoomDto.RoomMessage> SendText(string text)
        {
            var action = new RoomDto.RoomMessageAction();
            action.text = new RoomDto.RoomMessageActionText(text: text);
            return await this.SendUserAction(action);
        }

        public async Task<RoomDto.RoomMessage> SendSpeech(byte[] speech)
        {
            var action = new RoomDto.RoomMessageAction();
            action.speech = new RoomDto.RoomMessageActionSpeech(speech);
            return await this.SendUserAction(action);
        }

        public async Task<RoomDto.RoomMessage> SendNamedAction(string name, string value = null)
        {
            var action = new RoomDto.RoomMessageAction();
            action.namedAction = new RoomDto.RoomMessageNamedAction(name, value);
            return await this.SendUserAction(action);
        }

        private async Task<RoomDto.RoomMessage> SendUserAction(RoomDto.RoomMessageAction userAction)
        {
            var roomMessagePost = new RoomDto.RoomMessagePost
            {
                endUserId = this.endUserId,
                action = userAction
            };

            return await this.Post<RoomDto.RoomMessage>($"/{this.roomId}/messages", roomMessagePost);
        }

        public async Task<RoomDto.RoomMessagesApiResponse> PollNewMessages()
        {
            var roomResponse = await Get<RoomDto.Room>($"/{this.roomId}");

            var modifiedAt = ParseServerTimeString(roomResponse.modifiedAt);

            if (this.lastRoomModifiedAt >= modifiedAt)
            {
                // This situation shouldn't happen - this SDK version shouldn't be used anymore
                // TODO perhaps we should stop polling in this case
                return null;
            }

            var response =
                await Get<RoomDto.RoomMessagesApiResponse>(
                    $"/{this.roomId}/messages?sinceGt={GetServerTimeString(this.lastPollingMessageInstant)}");

            if (response != null)
            {
                UnityEngine.Debug.Log($"modified at: {this.lastRoomModifiedAt}, polling instant {this.lastPollingMessageInstant}");
                // We update the last room change time only if we checked for new messages
                this.lastRoomModifiedAt = modifiedAt;

                if (response.results.Count > 0)
                {
                    DateTime messageInstant = ParseServerTimeString(response.results[0].instant);
                    if (messageInstant >= this.lastPollingMessageInstant)
                    {
                        this.lastPollingMessageInstant = messageInstant;
                    }
                }
            }

            return response;
        }

        public async Task<RoomDto.BeingVoiceData> GetRoomMessageVoiceData(RoomDto.RoomMessage roomMessage)
        {
            return await this.Get<RoomDto.BeingVoiceData>($"/{this.roomId}/messages/{roomMessage.id}/voice-data");
        }

        internal void OverrrideRoomId(string roomId)
        {
            this.roomId = roomId;
        }


        private async Task<T> Request<T>(string endpoint, string method, Dictionary<string, string> headers,
            object body = null)
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json")); //

            var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
            if (body != null)
            {
                var jsonBody = JsonConvert.SerializeObject(body);
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }

            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseData = JsonConvert.DeserializeObject<T>(responseJson);

            return responseData;
        }

        private static string GetServerTimeString(DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        private static DateTime ParseServerTimeString(string virbeDateString)
        {
            if (DateTime.TryParse(virbeDateString, out var date))
            {
                return date.ToUniversalTime();
            }
            else
            {
                UnityEngine.Debug.LogError($"Could not parse given date {virbeDateString} in {nameof(RoomApiService)}");
            }

            return DateTime.MinValue;
        }
    }
}