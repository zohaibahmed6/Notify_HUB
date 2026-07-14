namespace NotifyHub.Domain.Messaging;

/// P9-09: standard SMS segmentation math, mirroring what a real carrier computes and
/// returns in its delivery-receipt webhook — GSM-7 encoding if every character fits the
/// GSM 03.38 alphabet (basic + extension tables), else UCS-2.
public static class PduSegmentCalculator
{
    // GSM 03.38 default alphabet basic character set, plus the extension table (escape-
    // prefixed characters, e.g. the Euro sign) — together they determine whether the
    // whole message can encode as GSM-7 (any other character forces UCS-2).
    private const string GsmBasicCharacters =
        "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";
    private const string GsmExtendedCharacters = "^{}\\[~]|€";

    private const int Gsm7SingleSegmentLimit = 160;
    private const int Gsm7MultiSegmentLimit = 153;
    private const int Ucs2SingleSegmentLimit = 70;
    private const int Ucs2MultiSegmentLimit = 67;

    public static int CalculateSegmentCount(string text)
    {
        var isGsm7 = text.All(c => GsmBasicCharacters.Contains(c) || GsmExtendedCharacters.Contains(c));

        var singleSegmentLimit = isGsm7 ? Gsm7SingleSegmentLimit : Ucs2SingleSegmentLimit;
        var multiSegmentLimit = isGsm7 ? Gsm7MultiSegmentLimit : Ucs2MultiSegmentLimit;

        if (text.Length <= singleSegmentLimit)
            return 1;

        return (int)Math.Ceiling((double)text.Length / multiSegmentLimit);
    }
}
