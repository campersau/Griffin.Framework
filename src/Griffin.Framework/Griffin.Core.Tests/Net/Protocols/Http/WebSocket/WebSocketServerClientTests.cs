using FluentAssertions;
using Griffin.Core.Tests.Net.Channels;
using Griffin.Net.Buffers;
using Griffin.Net.Channels;
using Griffin.Net.Protocols.Http;
using Griffin.Net.Protocols.Http.WebSocket;
using System;
using System.IO;
using System.Threading;
using Xunit;

namespace Griffin.Core.Tests.Net.Protocols.Http.WebSocket
{
    public class WebSocketServerClientTests : IDisposable
    {
        private ClientServerHelper _helper;

        public WebSocketServerClientTests()
        {
            _helper = ClientServerHelper.Create();
        }

        [Fact]
        public void complete_server_client_communication()
        {
            object actual = null;

            var evt = new ManualResetEvent(false);

            var slice2 = new BufferSlice(new byte[65535], 0, 65535);
            var sut2 = new TcpChannel(slice2, new WebSocketEncoder(), new WebSocketDecoder());
            sut2.MessageReceived = (channel, message) =>
            {
                actual = message;
                evt.Set();
            };
            sut2.Assign(_helper.Server);

            var slice1 = new BufferSlice(new byte[65535], 0, 65535);
            var sut1 = new TcpChannel(slice1, new WebSocketEncoder(), new WebSocketDecoder());
            sut1.MessageReceived = (channel, message) =>
            {
                actual = message;
                evt.Set();
            };
            sut1.Assign(_helper.Client);

            // handshake
            sut1.Send(new WebSocketUpgradeRequest());

            evt.WaitOne(500).Should().BeTrue();
            evt.Reset();
            actual.Should().BeAssignableTo<IHttpRequest>();
            ((IHttpRequest)actual).Headers["Connection"].Should().Be("Upgrade");
            ((IHttpRequest)actual).Headers["Upgrade"].Should().Be("websocket");
            ((IHttpRequest)actual).Headers["Sec-WebSocket-Version"].Should().NotBeNullOrEmpty();
            ((IHttpRequest)actual).Headers["Sec-WebSocket-Key"].Should().NotBeNullOrEmpty();

            var webSocketKey = ((IHttpRequest)actual).Headers["Sec-WebSocket-Key"];
            sut2.Send(new WebSocketUpgradeResponse(webSocketKey));

            evt.WaitOne(500).Should().BeTrue();
            evt.Reset();
            actual.Should().BeAssignableTo<IHttpResponse>();
            ((IHttpResponse)actual).Headers["Sec-WebSocket-Accept"].Should().NotBeNullOrEmpty();

            // ping
            var expected = new byte[] { 1, 2, 3 };
            sut1.Send(new WebSocketMessage(WebSocketOpcode.Ping, new MemoryStream(expected)));

            evt.WaitOne(500).Should().BeTrue();
            evt.Reset();
            actual.Should().BeAssignableTo<IWebSocketMessage>();
            ((IWebSocketMessage)actual).Opcode.Should().Be(WebSocketOpcode.Ping);
            ((IWebSocketMessage)actual).Payload.ReadByte().Should().Be(expected[0]);
            ((IWebSocketMessage)actual).Payload.ReadByte().Should().Be(expected[1]);
            ((IWebSocketMessage)actual).Payload.ReadByte().Should().Be(expected[2]);

            // pong
            sut2.Send(new WebSocketMessage(WebSocketOpcode.Pong, new MemoryStream(expected)));

            evt.WaitOne(500).Should().BeTrue();
            evt.Reset();
            actual.Should().BeAssignableTo<IWebSocketMessage>();
            ((IWebSocketMessage)actual).Opcode.Should().Be(WebSocketOpcode.Pong);
            ((IWebSocketMessage)actual).Payload.ReadByte().Should().Be(expected[0]);
            ((IWebSocketMessage)actual).Payload.ReadByte().Should().Be(expected[1]);
            ((IWebSocketMessage)actual).Payload.ReadByte().Should().Be(expected[2]);
        }

        public void Dispose()
        {
            _helper.Dispose();
        }
    }
}
