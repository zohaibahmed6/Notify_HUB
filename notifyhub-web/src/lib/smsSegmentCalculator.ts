// Client-side preview of NotifyHub.Domain/Messaging/PduSegmentCalculator.cs — GSM-7 vs
// UCS-2 SMS segmentation, for live feedback while typing. Non-authoritative: the real
// PduCount is only ever computed server-side at send time (MockGatewayController.Send)
// from the message's actual RenderedBody. Keep the two character tables textually
// identical to the C# source so a future backend change is easy to diff against this file.
//
// Iterates by UTF-16 code unit (string indexing/`.length`), not by Unicode code point
// ([...text]/Array.from), to match C#'s `foreach (char c in text)`/`string.Length` — a
// surrogate-pair emoji is 2 "characters" to both, each independently failing the GSM-7
// membership check, which is what correctly forces UCS-2 for any astral character.
const GSM_BASIC_CHARACTERS =
  "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";
const GSM_EXTENDED_CHARACTERS = "^{}\\[~]|€";

const GSM7_SINGLE_SEGMENT_LIMIT = 160;
const GSM7_MULTI_SEGMENT_LIMIT = 153;
const UCS2_SINGLE_SEGMENT_LIMIT = 70;
const UCS2_MULTI_SEGMENT_LIMIT = 67;

export interface SmsSegmentInfo {
  isGsm7: boolean;
  segmentCount: number;
}

export function calculateSmsSegmentInfo(text: string): SmsSegmentInfo {
  let isGsm7 = true;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (!GSM_BASIC_CHARACTERS.includes(c) && !GSM_EXTENDED_CHARACTERS.includes(c)) {
      isGsm7 = false;
      break;
    }
  }

  const singleSegmentLimit = isGsm7 ? GSM7_SINGLE_SEGMENT_LIMIT : UCS2_SINGLE_SEGMENT_LIMIT;
  const multiSegmentLimit = isGsm7 ? GSM7_MULTI_SEGMENT_LIMIT : UCS2_MULTI_SEGMENT_LIMIT;

  const segmentCount =
    text.length <= singleSegmentLimit ? 1 : Math.ceil(text.length / multiSegmentLimit);

  return { isGsm7, segmentCount };
}
