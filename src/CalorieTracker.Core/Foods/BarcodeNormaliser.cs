namespace CalorieTracker.Core.Foods;

public static class BarcodeNormaliser
{
    /// <summary>
    /// Keeps the digits of <paramref name="input"/> (spaces, hyphens and any other characters are dropped).
    /// 8 and 13 digits are valid as they are, 12 digits (UPC-A) gain a leading 0, any other length is invalid.
    /// </summary>
    public static bool TryNormalise(string? input, out string barcode)
    {
        barcode = "";
        if (input is null)
        {
            return false;
        }

        var digits = new string(input.Where(char.IsAsciiDigit).ToArray());
        switch (digits.Length)
        {
            case 8:
            case 13:
                barcode = digits;
                return true;
            case 12:
                barcode = "0" + digits;
                return true;
            default:
                return false;
        }
    }
}
