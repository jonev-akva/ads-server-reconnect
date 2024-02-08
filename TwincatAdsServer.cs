namespace ServerReconnectTest;

using System.Text;
using TwinCAT.Ads;
using TwinCAT.Ads.Server;

public class TwincatAdsTestServer : AdsServer
{
    public TwincatAdsTestServer(ushort portNumber, string portName)
        : base(portNumber, portName)
    {
    }

    public string? LastReceivedData { get; private set; }
    
    public void ClearReceivedData()
    {
        this.LastReceivedData = null;
    }

    public async Task WaitForDataAsync(TimeSpan timeout)
    {
        var start = DateTime.Now;
        while (this.LastReceivedData == null)
        {
            if (start.Add(timeout) < DateTime.Now)
                throw new TimeoutException($"Failed to await receiving data in {nameof(TwincatAdsTestServer)}");

            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    public async Task ConnectServerWithRetryAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var start = DateTime.Now;
        while (!this.IsConnected && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var amsServerPort = this.ConnectServer();
            }
            catch (Exception e)
            {
                if (start.Add(timeout) < DateTime.Now)
                    throw new TimeoutException("Failed to await connect server", e);

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    protected override Task<ResultWrite> OnWriteAsync(
        AmsAddress target,
        uint invokeId,
        uint indexGroup,
        uint indexOffset,
        ReadOnlyMemory<byte> writeData,
        CancellationToken cancel)
    {
        this.LastReceivedData = Encoding.Default.GetString(writeData.Span.ToArray()).Trim('\0');

        return Task.FromResult(ResultWrite.CreateError(AdsErrorCode.NoError, invokeId));
    }
}