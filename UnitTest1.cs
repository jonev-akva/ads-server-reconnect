namespace ServerReconnectTest;

using System.Text;
using TwinCAT.Ads;
using TwinCAT.Ads.TcpRouter;
using Xunit.Abstractions;

public class UnitTest1
{
    private readonly ITestOutputHelper _output;

    public UnitTest1(ITestOutputHelper output)
    {
        _output = output;
    }
    // NB: Don't run the tests simultaneously
    
    // This test is for the following scenario
    // 0. Router, server and client is initialized
    // 1. Client is sending a message which should be directed to the server without errors.
    // 2. The server disconnects
    // 3. Sending an message while the server is disconnected. This has a inconsistent result. Sometimes it returns "NoError" and either Succeeded or Failed. Sometimes it returns Failed and TargetPortNotFound.
    // 4. The server connects again
    // 5. Sending a message to the server.
    //  - When 3. returns TargetPortNotFound, this also returns TargetPortNotFound - which means that the communication does not work after the reconnect.
    //  - When 3. returns "NoError", this returns Ok - which means that the communication works after reconnect
    // This test proves that when sending data to a server that is disconnected, sending data after it connects again will not work when sending returns "TargetPortNotFound"
    [Fact]
    public async Task Test1Async()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        // Setting up router
        using var routerLogger = _output.BuildLoggerFor<AmsTcpIpRouter>();
        var router = new AmsTcpIpRouter(new AmsNetId("10.10.10.10.1.1"));
        var routerTask = router.StartAsync(cts.Token);
        await Task.Delay(1000);

        try
        {
            // Starting server and connecting to router
            var adsServer = new TwincatAdsTestServer(
                45086, "TestServer");
            await adsServer.ConnectServerWithRetryAsync(TimeSpan.FromSeconds(5), default);
            
            // Initializing client
            using var adsClientLogger = _output.BuildLoggerFor<AmsTcpIpRouter>();
            using var adsClient = new AdsClient(adsClientLogger);
            adsClient.Connect(new AmsAddress("10.10.10.10.1.1", 45086));
            
            // 1. Sending message to server OK
            _output.WriteLine("----- First message -----");
            var payload = "First message";
            var writeResult = await adsClient.WriteAsync(
                    0,
                    1,
                    Encoding.Default.GetBytes(payload + "\0"),
                    cts.Token)
                .ConfigureAwait(false);
            Assert.True(writeResult.Succeeded);
            Assert.False(writeResult.Failed);
            await adsServer.WaitForDataAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(payload, adsServer.LastReceivedData);
            _output.WriteLine("----------");
            
            // 2. Simulating server goes down
            _output.WriteLine("----- Ads server disconnect -----");
            var disconnected = adsServer.Disconnect();
            _output.WriteLine($"----Disconnected; {disconnected}------");

            // 3. Sending message to server Fails since the server is down
            _output.WriteLine("----- Second message -----");
            adsServer.ClearReceivedData();
            payload = "Second message";
            writeResult = await adsClient.WriteAsync(
                    0,
                    1,
                    Encoding.Default.GetBytes(payload + "\0"),
                    cts.Token)
                .ConfigureAwait(false);
            _output.WriteLine($"Write status; {writeResult.ErrorCode}");
            Assert.False(writeResult.Succeeded);
            Assert.True(writeResult.Failed);
            Assert.Equal(AdsErrorCode.TargetPortNotFound, writeResult.ErrorCode);
            _output.WriteLine("----------");
            await Task.Delay(500);
            
            // 4. Simulating server comes up again
            _output.WriteLine("----- Ads server Connect -----");
            await adsServer.ConnectServerWithRetryAsync(TimeSpan.FromSeconds(5), cts.Token);
            _output.WriteLine("----------");
            
            // 5. Sending message to server after server has been restarted AND the client has sent a message to the server while it was down
            _output.WriteLine("----- Third message -----");
            adsServer.ClearReceivedData();
            payload = "Third message";
            writeResult = await adsClient.WriteAsync(
                    0x0,
                    1,
                    Encoding.Default.GetBytes(payload + "\0"),
                    cts.Token)
                .ConfigureAwait(false);
            _output.WriteLine($"Write status; {writeResult.ErrorCode}");
            Assert.True(writeResult.Succeeded);
            Assert.False(writeResult.Failed);
            await adsServer.WaitForDataAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(payload, adsServer.LastReceivedData);
            _output.WriteLine("----------");
        }
        finally
        {
            router.Stop();
            await routerTask;
        }
    }
    
    // This test is for the following scenario
    // 0. Router, server and client is initialized
    // 1. Client is sending a message which should be directed to the server without errors.
    // 2. The server disconnects
    // 3. The server connects again
    // 4. Sending a message to the server
    // This test proves that when not sending data to a server that is disconnected, sending after it connects will work
    [Fact]
    public async Task Test2Async()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        // Setting up router
        using var routerLogger = _output.BuildLoggerFor<AmsTcpIpRouter>();
        var router = new AmsTcpIpRouter(new AmsNetId("10.10.10.10.1.1"));
        var routerTask = router.StartAsync(cts.Token);
        await Task.Delay(1000);

        try
        {
            // Starting server and connecting to router
            var adsServer = new TwincatAdsTestServer(
                45086, "TestServer");
            await adsServer.ConnectServerWithRetryAsync(TimeSpan.FromSeconds(5), default);
            
            // Initializing client
            using var adsClientLogger = _output.BuildLoggerFor<AmsTcpIpRouter>();
            using var adsClient = new AdsClient(adsClientLogger);
            adsClient.Connect(new AmsAddress("10.10.10.10.1.1", 45086));
            
            // 1. Sending message to server OK
            _output.WriteLine("----- First message -----");
            adsServer.ClearReceivedData();
            var payload = "First message";
            var writeResult = await adsClient.WriteAsync(
                    0,
                    1,
                    Encoding.Default.GetBytes(payload + "\0"),
                    cts.Token)
                .ConfigureAwait(false);
            Assert.True(writeResult.Succeeded);
            Assert.False(writeResult.Failed);
            await adsServer.WaitForDataAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(payload, adsServer.LastReceivedData);
            _output.WriteLine("----------");
            
            // 2. Simulating server goes down
            _output.WriteLine("----- Ads server disconnect -----");
            var disconnected = adsServer.Disconnect();
            _output.WriteLine($"----Disconnected; {disconnected}------");
            
            // 3. Simulating server comes up again
            _output.WriteLine("----- Ads server Connect -----");
            await adsServer.ConnectServerWithRetryAsync(TimeSpan.FromSeconds(5), cts.Token);
            _output.WriteLine("----------");
            
            // 4. Sending message to server after server has been restarted AND the client has sent a message to the server while it was down
            _output.WriteLine("----- Third message -----");
            adsServer.ClearReceivedData();
            payload = "Third message";
            writeResult = await adsClient.WriteAsync(
                    0x0,
                    1,
                    Encoding.Default.GetBytes(payload + "\0"),
                    cts.Token)
                .ConfigureAwait(false);
            _output.WriteLine($"Write status; {writeResult.ErrorCode}");
            Assert.True(writeResult.Succeeded);
            Assert.False(writeResult.Failed);
            await adsServer.WaitForDataAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(payload, adsServer.LastReceivedData);
            _output.WriteLine("----------");
        }
        finally
        {
            router.Stop();
            await routerTask;
        }
    }
    
    
    // This test is for the following scenario
    // 0. Router, server and client is initialized
    // 1. Client is sending a message which should be directed to the server without errors.
    // 2. The server disconnects
    // 3. Sending an message while the server is disconnected. Returns "NoError" and either Succeeded or Failed.
    // Link Github issue; https://github.com/Beckhoff/TF6000_ADS_DOTNET_V5_Samples/issues/53
    [Fact]
    public async Task ForGithubIssue53()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        // Setting up router
        using var routerLogger = _output.BuildLoggerFor<AmsTcpIpRouter>();
        var router = new AmsTcpIpRouter(new AmsNetId("10.10.10.10.1.1"));
        var routerTask = router.StartAsync(cts.Token);
        await Task.Delay(5000);

        try
        {
            // Starting server and connecting to router
            var adsServer = new TwincatAdsTestServer(
                45086, "TestServer");
            await adsServer.ConnectServerWithRetryAsync(TimeSpan.FromSeconds(5), default);
            await Task.Delay(5000);
            
            // Initializing client
            using var adsClientLogger = _output.BuildLoggerFor<AmsTcpIpRouter>();
            using var adsClient = new AdsClient(adsClientLogger);
            adsClient.Connect(new AmsAddress("10.10.10.10.1.1", 45086));
            await Task.Delay(5000);
            
            // 1. Sending message to server OK
            _output.WriteLine("----- First message -----");
            var payload = "First message";
            var writeResult = await adsClient.WriteAsync(
                    0,
                    1,
                    Encoding.Default.GetBytes(payload + "\0"),
                    cts.Token)
                .ConfigureAwait(false);
            Assert.True(writeResult.Succeeded);
            Assert.False(writeResult.Failed);
            await adsServer.WaitForDataAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(payload, adsServer.LastReceivedData);
            _output.WriteLine("----------");
            
            await Task.Delay(5000);
            // 2. Simulating server goes down
            _output.WriteLine("----- Ads server disconnect -----");
            var disconnected = adsServer.Disconnect();
            _output.WriteLine($"----Disconnected; {disconnected}------");
            await Task.Delay(5000);
            
            // 3. Sending message to server Fails since the server is down
            _output.WriteLine("----- Second message -----");
            payload = "Second message";
            writeResult = await adsClient.WriteAsync(
                    0,
                    1,
                    Encoding.Default.GetBytes(payload + "\0"),
                    cts.Token)
                .ConfigureAwait(false);
            _output.WriteLine($"Write status; {writeResult.ErrorCode}");
            Assert.False(writeResult.Succeeded);
            Assert.True(writeResult.Failed);
            Assert.Equal(AdsErrorCode.TargetPortNotFound, writeResult.ErrorCode);
            await adsServer.WaitForDataAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(payload, adsServer.LastReceivedData);
            _output.WriteLine("----------");
        }
        finally
        {
            router.Stop();
            await routerTask;
        }
    }
}