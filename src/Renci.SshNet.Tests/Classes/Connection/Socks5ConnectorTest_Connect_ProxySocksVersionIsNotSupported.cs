﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Renci.SshNet.Connection;
using Renci.SshNet.Common;
using Renci.SshNet.Tests.Common;
using System;
using System.Net;
using System.Net.Sockets;

namespace Renci.SshNet.Tests.Classes.Connection
{
    [TestClass]
    public class Socks5ConnectorTest_Connect_ProxySocksVersionIsNotSupported : Socks5ConnectorTestBase
    {
        private ConnectionInfo _connectionInfo;
        private ProxyConnectionInfo _proxyConnectionInfo;
        private AsyncSocketListener _proxyServer;
        private Socket _clientSocket;
        private IConnector _proxyConnector;
        private byte _proxySocksVersion;
        private bool _disconnected;
        private ProxyException _actualException;

        protected override void SetupData()
        {
            base.SetupData();

            _connectionInfo = CreateConnectionInfo("proxyUser", "proxyPwd");
            _proxyConnectionInfo = (ProxyConnectionInfo)_connectionInfo.ProxyConnection;
            _connectionInfo.Timeout = TimeSpan.FromMilliseconds(100);
            _proxySocksVersion = GetNotSupportedSocksVersion();
            _actualException = null;

            _clientSocket = SocketFactory.Create(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _proxyConnector = ServiceFactory.CreateConnector(_proxyConnectionInfo, SocketFactory);

            _proxyServer = new AsyncSocketListener(new IPEndPoint(IPAddress.Loopback, _proxyConnectionInfo.Port));
            _proxyServer.Disconnected += socket => _disconnected = true;
            _proxyServer.BytesReceived += (bytesReceived, socket) =>
                {
                    socket.Send(new byte[] { _proxySocksVersion });
                };
            _proxyServer.Start();
        }

        protected override void SetupMocks()
        {
            SocketFactoryMock.Setup(p => p.Create(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                             .Returns(_clientSocket);
            ServiceFactoryMock.Setup(p => p.CreateConnector(_proxyConnectionInfo, SocketFactory))
                              .Returns(_proxyConnector);
        }

        protected override void TearDown()
        {
            base.TearDown();

            if (_proxyServer != null)
            {
                _proxyServer.Dispose();
            }

            if (_clientSocket != null)
            {
                _clientSocket.Dispose();
            }

            if (_proxyConnector != null)
            {
                _proxyConnector.Dispose();
            }
        }

        protected override void Act()
        {
            try
            {
                Connector.Connect(_connectionInfo);
                Assert.Fail();
            }
            catch (ProxyException ex)
            {
                _actualException = ex;
            }
        }

        [TestMethod]
        public void ConnectShouldHaveThrownProxyException()
        {
            Assert.IsNotNull(_actualException);
            Assert.IsNull(_actualException.InnerException);
            Assert.AreEqual(string.Format("SOCKS Version '{0}' is not supported.", _proxySocksVersion), _actualException.Message);
        }

        [TestMethod]
        public void ConnectionToProxyShouldHaveBeenShutDown()
        {
            Assert.IsTrue(_disconnected);
        }

        [TestMethod]
        public void ClientSocketShouldHaveBeenDisposed()
        {
            try
            {
                _clientSocket.Receive(new byte[0]);
                Assert.Fail();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        [TestMethod]
        public void CreateOnSocketFactoryShouldHaveBeenInvokedOnce()
        {
            SocketFactoryMock.Verify(p => p.Create(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp),
                                     Times.Once());
        }

        private static byte GetNotSupportedSocksVersion()
        {
            var random = new Random();

            while (true)
            {
                var socksVersion = random.Next(1, 255);
                if (socksVersion != 5)
                {
                    return (byte) socksVersion;
                }
            }
        }
    }
}
