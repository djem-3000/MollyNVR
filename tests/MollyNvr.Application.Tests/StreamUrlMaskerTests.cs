using MollyNvr.Application.Streams;

namespace MollyNvr.Application.Tests;

public class StreamUrlMaskerTests
{
    [Theory]
    [InlineData("rtsp://user:secret@192.168.1.10:554/main", "rtsp://user:***@192.168.1.10:554/main")]
    [InlineData("rtsp://user:secret@192.168.1.10", "rtsp://user:***@192.168.1.10")]
    [InlineData("ffmpeg:rtsp://admin:pass@192.168.1.10/main#video=h264", "ffmpeg:rtsp://admin:***@192.168.1.10/main#video=h264")]
    [InlineData("rtsp://user@192.168.1.10/main", "rtsp://user@192.168.1.10/main")]
    [InlineData("rtsp://192.168.1.10/main", "rtsp://192.168.1.10/main")]
    [InlineData("ffmpeg:virtual?video#video=h264", "ffmpeg:virtual?video#video=h264")]
    public void Mask_MasksPasswordOnly(string url, string expected)
    {
        Assert.Equal(expected, StreamUrlMasker.Mask(url));
    }
}
