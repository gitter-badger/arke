﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading.Tasks;
using Arke.IVR.Bridging;
using Arke.SipEngine.Api;
using Arke.SipEngine.Bridging;
using Arke.SipEngine.CallObjects.RecordingFiles;
using Arke.SipEngine.Events;
using AsterNET.ARI;
using NLog;

namespace Arke.IVR
{
    [SuppressMessage("ReSharper", "FormatStringProblem", Justification = "NLog will use args in the output format instead of string format.")]
    public class ArkeSipApiClient : ISipApiClient, ISipBridgingApi, ISipLineApi, ISipPromptApi, ISipRecordingApi
    {
        private readonly IAriClient _ariClient;
        private string _appName;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public ArkeSipApiClient(IAriClient ariClient)
        {
            _ariClient = ariClient;
            _appName = ArkeCallFlowService.Configuration.GetSection("appSettings:AsteriskAppName").Value;
        }

        public event DtmfReceivedEventHandler OnDtmfReceivedEvent;
        public event LineHangupEventHandler OnLineHangupEvent;
        public event PromptPlaybackFinishedEventHandler OnPromptPlaybackFinishedEvent;

        public async Task AnswerLine(string lineId)
        {
            await _ariClient.Channels.AnswerAsync(lineId);
        }

        public async Task PlayMusicOnHoldToLine(string channelId)
        {
            try
            {
                await _ariClient.Channels.StartMohAsync(channelId);
            }
            catch (HttpRequestException e)
            {
                _logger.Warn(e, "Channel probably dead");
            }
        }

        public async Task StopMusicOnHoldToLine(string channelId)
        {
            try
            {
                await _ariClient.Channels.StopMohAsync(channelId);
            }
            catch (HttpRequestException e)
            {
                _logger.Warn(e, "Channel probably dead");
            }
        }

        public async Task<string> GetLineVariable(string channelId, string variableName)
        {
            var variable = await _ariClient.Channels.GetChannelVarAsync(channelId, variableName);
            return variable.Value;
        }

        public async Task<string> GetEndpoint(string lineId)
        {
            var getChannelVarResult = await _ariClient.Channels.GetChannelVarAsync(lineId, "CHANNEL(pjsip,remote_addr)");
            _logger.Debug("GetEndpoint", new {LineId = lineId, Result = getChannelVarResult.Value});
            return getChannelVarResult.Value.Split(':')[0];
        }

        public async Task<IBridge> CreateBridge(string bridgeType, string bridgeName)
        {
            var asteriskBridge = await _ariClient.Bridges.CreateAsync(bridgeType, Guid.NewGuid().ToString(), bridgeName);
            var artemisBridge = new ArkeBridge()
            {
                Id = asteriskBridge.Id,
                Type = asteriskBridge.Bridge_type
            };
            return artemisBridge;
        }

        public async Task AddLineToBridge(string lineId, string bridgeId)
        {
            await _ariClient.Bridges.AddChannelAsync(bridgeId, lineId);
        }

        public async Task RemoveLineFromBridge(string lineId, string bridgeId)
        {
            await _ariClient.Bridges.RemoveChannelAsync(bridgeId, lineId);
        }

        public async Task DestroyBridge(string bridgeId)
        {
            await _ariClient.Bridges.DestroyAsync(bridgeId);
        }

        public async Task HangupLine(string lineId)
        {
            try
            {
                await _ariClient.Channels.HangupAsync(lineId, "normal");
            }
            catch (HttpRequestException e)
            {
                _logger.Warn(e, "Channel probably dead");
            }
        }

        public async Task StopRecording(string recordingId)
        {
            await _ariClient.Recordings.StopAsync(recordingId);
        }

        public async Task<string> StartRecordingOnLine(string lineId, string fileName)
        {
            var snoopingChannel = await _ariClient.Channels.SnoopChannelAsync(lineId,
                _appName,
                "in",
                null,
                "SnoopChannel");
            var channelRecording = await _ariClient.Channels.RecordAsync(snoopingChannel.Id, fileName,
                new ArkeFileFormatFactory().GetFileFormat(), 0, 0, "fail", false, "none");
            return channelRecording.Name;
        }

        public async Task<string> StartRecordingOnBridge(string bridgeId, string fileName)
        {
            var recording = await _ariClient.Bridges.RecordAsync(bridgeId,
                fileName,
                new ArkeFileFormatFactory().GetFileFormat(),
                0,
                0,
                "fail",
                false,
                "none");
            return recording.Name;
        }

        public async Task StopRecordingOnBridge(string recordingId)
        {
            await _ariClient.Recordings.StopAsync(recordingId);
        }

        public async Task<string> PlayPromptToLine(string lineId, string promptFile, string languageCode)
        {
            try
            {
                return (await _ariClient.Channels.PlayAsync(lineId, $"sound:{promptFile}", languageCode)).Id;
            }
            catch (HttpRequestException e)
            {
                _logger.Warn(e, "Channel probably dead");
                return "";
            }
        }

        public async Task<string> PlayPromptToBridge(string bridgeId, string promptFile, string languageCode)
        {
            return (await _ariClient.Bridges.PlayAsync(bridgeId, $"sound:{promptFile}", languageCode)).Id;
        }

        public async Task StopPrompt(string playbackId)
        {
            try
            {
                await _ariClient.Playbacks.StopAsync(playbackId);
            }
            catch (HttpRequestException e)
            {
                _logger.Warn(e, "Channel probably dead");
            }
        }

        public async Task PlayMusicOnHoldToBridge(string bridgeId)
        {
            await _ariClient.Bridges.StartMohAsync(bridgeId);
        }

        public async Task<object> CreateOutboundCall(string numberToDial)
        {
            try
            {
                return _ariClient.Channels.Originate(
                    "PJSIP/sipoutbound",
                    numberToDial,
                    app: _appName, // need to test if this is needed
                    appArgs: "dialed");
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error creating an outbound call");
                throw;
            }
        }

        public async Task<string> GetLineState(string lineId)
        {
            return (await _ariClient.Channels.GetAsync(lineId)).State;
        }

        public void SubscribeToDtmfReceivedEvents()
        {
            _ariClient.OnChannelDtmfReceivedEvent += AriClient_OnChannelDtmfReceivedEvent;
        }

        public void SubscribeToPlaybackFinishedEvents()
        {
            _ariClient.OnPlaybackFinishedEvent += AriClient_OnPlaybackFinishedEvent;
        }

        public void UnsubscribeToDtmfReceivedEvents()
        {
            _ariClient.OnChannelDtmfReceivedEvent -= AriClient_OnChannelDtmfReceivedEvent;
        }

        public void UnsubscribeToPlaybackFinishedEvents()
        {
            _ariClient.OnPlaybackFinishedEvent -= AriClient_OnPlaybackFinishedEvent;
        }

        public void SubscribeToLineHangupEvents()
        {
            _ariClient.OnStasisEndEvent += AriClient_OnStasisEndEvent;
        }

        public void SetAppNameForEvents(string appName)
        {
            _appName = appName;
        }

        private void AriClient_OnStasisEndEvent(IAriClient sender, AsterNET.ARI.Models.StasisEndEvent e)
        {
            OnLineHangupEvent?.Invoke(this, new LineHangupEvent()
            {
                LineId = e.Channel.Id
            });
        }

        private void AriClient_OnPlaybackFinishedEvent(IAriClient sender, AsterNET.ARI.Models.PlaybackFinishedEvent e)
        {
            OnPromptPlaybackFinishedEvent?.Invoke(this, new PromptPlaybackFinishedEvent()
            {
                PlaybackId = e.Playback.Id
            });
        }

        private void AriClient_OnChannelDtmfReceivedEvent(IAriClient sender, AsterNET.ARI.Models.ChannelDtmfReceivedEvent e)
        {
            OnDtmfReceivedEvent?.Invoke(this, new DtmfReceivedEvent()
            {
                Digit = e.Digit,
                DurationInMilliseconds = e.Duration_ms,
                LineId = e.Channel.Id
            });
        }
    }
}
