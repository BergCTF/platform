using System.Text;

namespace Berg.Api.Services;

public interface IDynamicFlagExecutableService
{
    byte[] GenerateExecutable(string flag);
}

public class DynamicFlagExecutableService(
    ILogger<DynamicFlagExecutableService> logger
) : IDynamicFlagExecutableService
{

    // Taken from https://github.com/tchajed/minimal-elf
    private static readonly byte[] MinimalElfHeader = [
        0x7f, 0x45, 0x4c, 0x46, 0x02, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x3e, 0x00, 0x01, 0x00, 0x00, 0x00,
        0x78, 0x00, 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x38, 0x00, 0x01, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00,
        0x78, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x78, 0x00, 0x40, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];

    // Taken from https://github.com/tchajed/minimal-elf
    private static readonly byte[] ExitShellcode = [
        0x6a, 0x3c, // pushq $60
        0x58,       // popq %rax
        0x31, 0xff, // xorl %edi, %edi
        0x0f, 0x05, // syscall
    ];

    /// <summary>
    /// Generates a statically linked ELF binary that prints the flag when executed.
    /// Thanks to some ELF trickery, the generated files are very small: 145 bytes + flag byte length
    /// </summary>
    /// <param name="flag">The flag to print</param>
    /// <returns>A statically linked ELF binary that prints the flag when executed</returns>
    public byte[] GenerateExecutable(string flag)
    {
        using var activity = Constants.BergActivitySource.StartActivity();
        logger.LogDebug("Generating dynamic flag executable with flag: {Flag}", flag);

        var flagBytes = Encoding.UTF8.GetBytes(flag+"\n");
        var flagLength = flagBytes.Length;
        if (flagLength > 0xffff)
            throw new ArgumentOutOfRangeException(nameof(flag), "Flag too long");

        var l_hi = (byte)((flagBytes.Length >> 8) & 0xff);
        var l_lo = (byte)(flagBytes.Length & 0xff);
        using var outputStream = new MemoryStream();
        outputStream.Write(MinimalElfHeader);

        byte[] printCode = [
            0x6a, 0x01,                   // push $0x1
            0x58,                         // pop  %rax
            0x66, 0xba, l_lo, l_hi,       // mov length,%dx
            0x40, 0xb7, 0x01,             // mov $0x1,%dil
            0xbe, 0x90, 0x00, 0x40, 0x00, // mov $0x400090,%esi
            0x0f, 0x05,                   // syscall
        ];
        outputStream.Write(printCode);
        outputStream.Write(ExitShellcode);
        outputStream.Write(flagBytes);
        return outputStream.ToArray();
    }
}