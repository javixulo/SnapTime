using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SnapTime.Domain.Enums;
using SnapTime.Infrastructure.Services;

// [F1-US-009]
namespace SnapTime.Tests.Metadata;

public class MetadataExtractorIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MetadataExtractorService _service;

    public MetadataExtractorIntegrationTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("snaptime-test-").FullName;
        var logger = Substitute.For<ILogger<MetadataExtractorService>>();
        _service = new MetadataExtractorService(logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ExtractAsync_JpegWithFullExif_ReturnsFourExifEntries()
    {
        var filePath = Path.Combine(_tempDir, "photo-full-exif.jpg");
        var jpegBytes = CreateJpegWithExif(
            dateTimeOriginal: "2024:08:15 14:30:00",
            subSecTimeOriginal: "10",
            dateTimeDigitized: "2024:08:15 15:30:00",
            dateTime: "2024:08:15 16:30:00");
        await File.WriteAllBytesAsync(filePath, jpegBytes);

        var results = await _service.ExtractAsync(filePath, MediaType.Image, CancellationToken.None);

        results.Should().HaveCount(4);
        results.Should().AllSatisfy(e => e.Source.Should().Be("exif"));
        results.Should().ContainSingle(e => e.Tag == "Exif SubIFD:Date/Time Original" && e.Value == "2024:08:15 14:30:00");
        results.Should().ContainSingle(e => e.Tag == "Exif SubIFD:Sub-Sec Time Original" && e.Value == "10");
        results.Should().ContainSingle(e => e.Tag == "Exif IFD0:Date/Time Digitized" && e.Value == "2024:08:15 15:30:00");
        results.Should().ContainSingle(e => e.Tag == "Exif IFD0:Date/Time" && e.Value == "2024:08:15 16:30:00");
    }

    [Fact]
    public async Task ExtractAsync_JpegWithoutExifDateMetadata_ReturnsEmptyList()
    {
        var filePath = Path.Combine(_tempDir, "photo-no-exif.jpg");
        var jpegBytes = CreateMinimalJpeg();
        await File.WriteAllBytesAsync(filePath, jpegBytes);

        var results = await _service.ExtractAsync(filePath, MediaType.Image, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_MovWithQuickTimeDates_ReturnsUpToFiveQuickTimeEntries()
    {
        var filePath = Path.Combine(_tempDir, "video-quicktime.mov");
        var movBytes = CreateMovWithQuickTimeDates(
            createDate: new DateTime(2024, 8, 15, 14, 30, 0, DateTimeKind.Utc),
            modifyDate: new DateTime(2024, 8, 15, 15, 30, 0, DateTimeKind.Utc),
            trackCreateDate: new DateTime(2024, 8, 15, 16, 30, 0, DateTimeKind.Utc),
            mediaCreateDate: new DateTime(2024, 8, 15, 17, 30, 0, DateTimeKind.Utc));
        await File.WriteAllBytesAsync(filePath, movBytes);

        var results = await _service.ExtractAsync(filePath, MediaType.Video, CancellationToken.None);

        results.Should().NotBeEmpty();
        results.Should().HaveCountLessThanOrEqualTo(5);
        results.Should().AllSatisfy(e => e.Source.Should().Be("quicktime"));
    }

    [Fact]
    public async Task ExtractAsync_JpegWithNonStandardDateFormat_DoesNotCrashAndReturnsEntryWithValue()
    {
        var filePath = Path.Combine(_tempDir, "photo-bad-date.jpg");
        var jpegBytes = CreateJpegWithExif(
            dateTimeOriginal: "2024/08/15 14:30:00",
            subSecTimeOriginal: null,
            dateTimeDigitized: null,
            dateTime: null);
        await File.WriteAllBytesAsync(filePath, jpegBytes);

        var results = await _service.ExtractAsync(filePath, MediaType.Image, CancellationToken.None);

        results.Should().ContainSingle(e => e.Tag == "Exif SubIFD:Date/Time Original");
        var entry = results.Should().ContainSingle(e => e.Tag == "Exif SubIFD:Date/Time Original").Subject;
        entry.Value.Should().Be("2024/08/15 14:30:00");
    }

    [Fact]
    public async Task ExtractAsync_CorruptImageFile_ReturnsEmptyList() =>
        await AssertCorruptFileExtractsToEmpty("corrupt-image.bin", 1024, MediaType.Image);

    [Fact]
    public async Task ExtractAsync_CorruptVideoFile_ReturnsEmptyList() =>
        await AssertCorruptFileExtractsToEmpty("corrupt-video.bin", 2048, MediaType.Video);

    private async Task AssertCorruptFileExtractsToEmpty(string fileName, int size, MediaType mediaType)
    {
        var filePath = Path.Combine(_tempDir, fileName);
        var randomBytes = new byte[size];
        Random.Shared.NextBytes(randomBytes);
        await File.WriteAllBytesAsync(filePath, randomBytes);

        var results = await _service.ExtractAsync(filePath, mediaType, CancellationToken.None);

        results.Should().BeEmpty();
    }

    private static byte[] CreateJpegWithExif(
        string? dateTimeOriginal,
        string? subSecTimeOriginal,
        string? dateTimeDigitized,
        string? dateTime)
    {
        var exifBytes = BuildExifApp1Data(
            dateTimeOriginal,
            subSecTimeOriginal,
            dateTimeDigitized,
            dateTime);

        var app1Length = 2 + exifBytes.Length;
        using var ms = new MemoryStream();

        ms.WriteByte(0xFF);
        ms.WriteByte(0xD8);

        ms.WriteByte(0xFF);
        ms.WriteByte(0xE1);
        ms.WriteByte((byte)(app1Length >> 8));
        ms.WriteByte((byte)app1Length);
        ms.Write(exifBytes);

        ms.WriteByte(0xFF);
        ms.WriteByte(0xD9);

        return ms.ToArray();
    }

    private static byte[] BuildExifApp1Data(
        string? dateTimeOriginal,
        string? subSecTimeOriginal,
        string? dateTimeDigitized,
        string? dateTime)
    {
        var entries = new List<(ushort tag, byte[] valueBytes)>();
        var subIfdEntries = new List<(ushort tag, byte[] valueBytes)>();

        void AddIfdEntry(string? value, ushort tag, List<(ushort, byte[])> list)
        {
            if (value == null) return;
            var bytes = Encoding.ASCII.GetBytes(value + "\0");
            list.Add((tag, bytes));
        }

        AddIfdEntry(dateTimeDigitized, 0x9004, entries);
        AddIfdEntry(dateTime, 0x0132, entries);
        AddIfdEntry(dateTimeOriginal, 0x9003, subIfdEntries);
        AddIfdEntry(subSecTimeOriginal, 0x9291, subIfdEntries);

        using var ms = new MemoryStream();

        ms.Write("Exif\0\0"u8);

        int tiffBase = (int)ms.Position;

        ms.WriteByte(0x49);
        ms.WriteByte(0x49);
        ms.WriteByte(0x2A);
        ms.WriteByte(0x00);
        ms.WriteByte(0x08);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);

        WriteIfdEntries(ms, entries, subIfdEntries.Count > 0, tiffBase);
        WriteIfdEntries(ms, subIfdEntries, false, tiffBase);

        return ms.ToArray();
    }

    private static void WriteIfdEntries(MemoryStream ms, List<(ushort tag, byte[] valueBytes)> entries, bool addExifIfdPointer, int tiffBase)
    {
        int entryCount = entries.Count + (addExifIfdPointer ? 1 : 0);
        if (entryCount == 0) return;

        ms.WriteByte((byte)entryCount);
        ms.WriteByte((byte)(entryCount >> 8));

        long streamAfterEntry = ms.Position + entryCount * 12 + 4;
        int strOffset = (int)(streamAfterEntry - tiffBase);

        foreach (var (tag, valueBytes) in entries)
        {
            WriteTiffDirEntry(ms, tag, 2, valueBytes, ref strOffset, tiffBase);
        }

        if (addExifIfdPointer)
        {
            WriteTiffDirEntryRaw(ms, 0x8769, 4, 1, (uint)strOffset);
        }

        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);

        foreach (var (_, valueBytes) in entries)
        {
            if (valueBytes.Length > 4)
                ms.Write(valueBytes, 0, valueBytes.Length);
        }
    }

    private static void WriteTiffDirEntry(MemoryStream ms, ushort tag, ushort type, byte[] valueBytes, ref int strOffset, int tiffBase)
    {
        ms.WriteByte((byte)tag);
        ms.WriteByte((byte)(tag >> 8));
        ms.WriteByte((byte)type);
        ms.WriteByte((byte)(type >> 8));
        uint count = (uint)valueBytes.Length;
        ms.WriteByte((byte)count);
        ms.WriteByte((byte)(count >> 8));
        ms.WriteByte((byte)(count >> 16));
        ms.WriteByte((byte)(count >> 24));
        if (count <= 4)
        {
            for (int i = 0; i < 4; i++)
                ms.WriteByte(i < valueBytes.Length ? valueBytes[i] : (byte)0);
        }
        else
        {
            uint offset = (uint)strOffset;
            strOffset += valueBytes.Length;
            ms.WriteByte((byte)offset);
            ms.WriteByte((byte)(offset >> 8));
            ms.WriteByte((byte)(offset >> 16));
            ms.WriteByte((byte)(offset >> 24));
        }
    }

    private static void WriteTiffDirEntryRaw(MemoryStream ms, ushort tag, ushort type, uint count, uint value)
    {
        ms.WriteByte((byte)tag);
        ms.WriteByte((byte)(tag >> 8));
        ms.WriteByte((byte)type);
        ms.WriteByte((byte)(type >> 8));
        ms.WriteByte((byte)count);
        ms.WriteByte((byte)(count >> 8));
        ms.WriteByte((byte)(count >> 16));
        ms.WriteByte((byte)(count >> 24));
        ms.WriteByte((byte)value);
        ms.WriteByte((byte)(value >> 8));
        ms.WriteByte((byte)(value >> 16));
        ms.WriteByte((byte)(value >> 24));
    }

    private static byte[] CreateMinimalJpeg()
    {
        return [0xFF, 0xD8, 0xFF, 0xD9];
    }

    private static byte[] CreateMovWithQuickTimeDates(
        DateTime createDate,
        DateTime modifyDate,
        DateTime trackCreateDate,
        DateTime mediaCreateDate)
    {
        var epoch1904 = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        uint CreateQuickTimeDate(DateTime dt) =>
            (uint)(dt.ToUniversalTime() - epoch1904).TotalSeconds;

        var mvhd = CreateMvhdAtom(
            CreateQuickTimeDate(createDate),
            CreateQuickTimeDate(modifyDate));

        var tkhd = CreateTkhdAtom(
            CreateQuickTimeDate(trackCreateDate),
            CreateQuickTimeDate(trackCreateDate.AddHours(1)));

        var mdhd = CreateMdhdAtom(
            CreateQuickTimeDate(mediaCreateDate),
            CreateQuickTimeDate(mediaCreateDate.AddHours(1)));

        var hdlr = CreateHdlrAtom();
        var minf = CreateMinfAtom();
        var mdia = CreateContainerAtom("mdia", mdhd, hdlr, minf);
        var trak = CreateContainerAtom("trak", tkhd, mdia);
        var moov = CreateContainerAtom("moov", mvhd, trak);
        var ftyp = CreateFtypAtom();

        using var ms = new MemoryStream();
        ms.Write(ftyp, 0, ftyp.Length);
        ms.Write(moov, 0, moov.Length);
        return ms.ToArray();
    }

    private static byte[] CreateContainerAtom(string type, params byte[][] children)
    {
        var totalSize = 8;
        foreach (var child in children)
            totalSize += child.Length;

        using var ms = new MemoryStream();
        WriteBigEndian32(ms, (uint)totalSize);
        ms.Write(Encoding.ASCII.GetBytes(type), 0, 4);

        foreach (var child in children)
            ms.Write(child, 0, child.Length);

        return ms.ToArray();
    }

    private static byte[] CreateFtypAtom()
    {
        using var ms = new MemoryStream();
        WriteBigEndian32(ms, 20);
        ms.Write("ftyp"u8);
        ms.Write("qt  "u8);
        WriteBigEndian32(ms, 0x00000200);
        ms.Write("qt  "u8);
        return ms.ToArray();
    }

    private static byte[] CreateMvhdAtom(uint creationTime, uint modificationTime)
    {
        using var ms = new MemoryStream();
        WriteBigEndian32(ms, 108);
        ms.Write("mvhd"u8);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        WriteBigEndian32(ms, creationTime);
        WriteBigEndian32(ms, modificationTime);
        WriteBigEndian32(ms, 1000);
        WriteBigEndian32(ms, 0);
        WriteBigEndian32(ms, 0x00010000);
        ms.WriteByte(0x01);
        ms.WriteByte(0x00);
        ms.Write(new byte[10]);
        WriteMatrix(ms);
        ms.Write(new byte[24]);
        WriteBigEndian32(ms, 2);
        return ms.ToArray();
    }

    private static byte[] CreateTkhdAtom(uint creationTime, uint modificationTime)
    {
        using var ms = new MemoryStream();
        WriteBigEndian32(ms, 92);
        ms.Write("tkhd"u8);
        ms.WriteByte(0);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x0F);
        WriteBigEndian32(ms, creationTime);
        WriteBigEndian32(ms, modificationTime);
        WriteBigEndian32(ms, 1);
        ms.Write(new byte[4]);
        WriteBigEndian32(ms, 0);
        ms.Write(new byte[8]);
        ms.Write(new byte[2]);
        ms.Write(new byte[2]);
        ms.Write(new byte[2]);
        ms.Write(new byte[2]);
        WriteMatrix(ms);
        WriteBigEndian32(ms, 0x00010000);
        WriteBigEndian32(ms, 0x00010000);
        return ms.ToArray();
    }

    private static byte[] CreateMdhdAtom(uint creationTime, uint modificationTime)
    {
        using var ms = new MemoryStream();
        WriteBigEndian32(ms, 32);
        ms.Write("mdhd"u8);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        WriteBigEndian32(ms, creationTime);
        WriteBigEndian32(ms, modificationTime);
        WriteBigEndian32(ms, 1000);
        WriteBigEndian32(ms, 0);
        ms.WriteByte(0x55);
        ms.WriteByte(0xC4);
        ms.WriteByte(0);
        ms.WriteByte(0);
        return ms.ToArray();
    }

    private static byte[] CreateHdlrAtom()
    {
        using var ms = new MemoryStream();
        WriteBigEndian32(ms, 33);
        ms.Write("hdlr"u8);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        WriteBigEndian32(ms, 0);
        ms.Write("vide"u8);
        ms.Write("appl"u8);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        return ms.ToArray();
    }

    private static byte[] CreateMinfAtom()
    {
        var vmhd = CreateVmhdAtom();
        var dinf = CreateDinfAtom();
        var stbl = CreateStblAtom();
        return CreateContainerAtom("minf", vmhd, dinf, stbl);
    }

    private static byte[] CreateVmhdAtom()
    {
        using var ms = new MemoryStream();
        WriteBigEndian32(ms, 20);
        ms.Write("vmhd"u8);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0x01);
        ms.Write(new byte[8]);
        return ms.ToArray();
    }

    private static byte[] CreateDinfAtom()
    {
        var dref = CreateDrefAtom();
        return CreateContainerAtom("dinf", dref);
    }

    private static byte[] CreateDrefAtom()
    {
        using var ms = new MemoryStream();
        WriteBigEndian32(ms, 28);
        ms.Write("dref"u8);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        WriteBigEndian32(ms, 1);
        WriteBigEndian32(ms, 12);
        ms.Write("url "u8);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0x01);
        return ms.ToArray();
    }

    private static byte[] CreateStblAtom()
    {
        var stsd = CreateStsdAtom();
        return CreateContainerAtom("stbl", stsd);
    }

    private static byte[] CreateStsdAtom()
    {
        using var ms = new MemoryStream();
        WriteBigEndian32(ms, 16);
        ms.Write("stsd"u8);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        WriteBigEndian32(ms, 0);
        return ms.ToArray();
    }

    private static void WriteMatrix(MemoryStream ms)
    {
        WriteBigEndian32(ms, 0x00010000);
        ms.Write(new byte[4]);
        ms.Write(new byte[4]);
        ms.Write(new byte[4]);
        WriteBigEndian32(ms, 0x00010000);
        ms.Write(new byte[4]);
        ms.Write(new byte[4]);
        ms.Write(new byte[4]);
        WriteBigEndian32(ms, 0x40000000);
    }

    private static void WriteBigEndian32(MemoryStream ms, uint value)
    {
        ms.WriteByte((byte)(value >> 24));
        ms.WriteByte((byte)(value >> 16));
        ms.WriteByte((byte)(value >> 8));
        ms.WriteByte((byte)value);
    }
}
