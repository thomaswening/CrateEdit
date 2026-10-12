# ID3 tags in MP3 files: a guide for the code

This guide explains how MP3 tags are stored, byte by byte, and how CrateEdit reads and edits them. It is the background for `src/CrateEdit.Core/Tags/Id3/` and `Mp3TagWriter`, and for [ADR-0008](../adr/0008-own-id3v2-frame-editor.md).

## 1. Where tags live in an MP3 file

An MP3 file is mostly a long sequence of **audio frames**: small chunks of compressed sound, about 26 ms each. Each audio frame starts with a **sync word** of eleven 1-bits, so its first byte is `0xFF`. Players find the audio by looking for that pattern.

The MP3 format has no place for metadata. Tags were added by convention as extra blocks **before** and **after** the audio:

```
┌──────────────────┬────────────────────────────────────┬──────────────┐
│ ID3v2 tag        │ audio frames                       │ ID3v1 tag    │
│ start of file,   │ FF FB ..  FF FB ..  FF FB ..       │ last 128     │
│ variable size    │                                    │ bytes, opt.  │
└──────────────────┴────────────────────────────────────┴──────────────┘
```

Players skip the ID3v2 tag because its header states its length, and they recognise the ID3v1 tag by its `TAG` marker.

## 2. ID3v1: fixed positions

The last 128 bytes, if they start with `TAG`:

| Bytes | Content |
|---|---|
| 0–2 | `TAG` |
| 3–32 | title (30) |
| 33–62 | artist (30) |
| 63–92 | album (30) |
| 93–96 | year (4) |
| 97–126 | comment (30); in ID3v1.1, byte 125 is `00` and byte 126 is the track number |
| 127 | genre as a number (e.g. 35 = House) |

ID3v1 is very limited: Latin-1 only, 30 characters, no label, BPM or key. CrateEdit never adds it, but keeps an existing one consistent with edits (`Id3v1Tag`).

## 3. ID3v2: a header, frames, padding

### Example
This is a complete, small ID3v2.4 tag: a title, a BPM, a Serato GEOB frame and padding. It is 129 bytes in total. Offsets are hexadecimal.

```
0000  49 44 33 04 00 00 00 00 00 77 54 49 54 32 00 00  ID3......wTIT2..
0010  00 0E 00 00 03 42 6F 6F 67 69 65 20 4E 69 67 68  .....Boogie Nigh
0020  74 73 54 42 50 4D 00 00 00 04 00 00 03 31 31 35  tsTBPM.......115
0030  47 45 4F 42 00 00 00 2F 00 00 00 61 70 70 6C 69  GEOB.../...appli
0040  63 61 74 69 6F 6E 2F 6F 63 74 65 74 2D 73 74 72  cation/octet-str
0050  65 61 6D 00 00 53 65 72 61 74 6F 20 4D 61 72 6B  eam..Serato Mark
0060  65 72 73 32 00 01 01 41 51 00 00 00 00 00 00 00  ers2...AQ.......
0070  00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00  ................
0080  00                                               .
```

### The tag header (bytes 0000–0009): `Id3v2Header`

```
49 44 33 │ 04 │ 00 │ 00 │ 00 00 00 77
 I  D  3 │ver │rev │flags│ tag size (synchsafe)
         │2.4 │    │none │ = 119 bytes follow the header
```

- **Version:** CrateEdit supports 3 (ID3v2.3) and 4 (ID3v2.4).
- **Flags:** a byte of yes/no switches, one bit each, tested with `flags & mask`:
  - `0x80` unsynchronisation;
  - `0x40` extended header;
  - `0x20` experimental;
  - `0x10` footer (v2.4).

  CrateEdit refuses tags with unsynchronisation, an extended header or a footer (see section 5).
- **Tag size:** the number of bytes *after* the header, frames plus padding. Total tag length = 10 + size (+ 10 if there is a footer). `Id3v2Header.TotalSizeBytes` computes this.

### Synchsafe integers: `Synchsafe`
Sizes in the header (and frame sizes in v2.4) use only the lower 7 bits of each byte, so the top bit is always 0. This way no size byte can be `0xFF`, and a player scanning for audio can never mistake a size field for a sync word.

```
00 00 00 77  →  (0x00 << 21) | (0x00 << 14) | (0x00 << 7) | 0x77  =  119
```

A byte ≥ `0x80` in a synchsafe field is invalid. `Synchsafe.IsValid` checks that no byte has its top bit set.

### Frames: `Id3v2Frame`
After the header, the tag is a sequence of **frames**. A frame is one labelled box of data. Each frame has a 10-byte **frame header** followed by its **body**:

```
54 49 54 32 │ 00 00 00 0E │ 00 00 │ 03 42 6F 6F 67 69 65 20 4E 69 67 68 74 73
 T  I  T  2 │ body size   │ flags │ body (14 bytes)
  frame id  │ = 14        │       │ 03 = UTF-8, then "Boogie Nights"
```

- **Frame id:** four characters A–Z/0–9 that say what the frame is.
- **Body size:** synchsafe in v2.4, a plain 32-bit big-endian number in v2.3.
- **Frame flags:** two bytes. Some only carry information (e.g. "discard if the audio changes"); others change how the body is stored (compressed, encrypted, unsynchronised). `Id3v2Frame.HasFormatFlags` detects the latter.

There is **no table of contents**. To find all frames, you walk the tag:

1. Start right after the tag header (offset `000A`).
2. Read 10 bytes. These are the frame header: the id and the body size.
3. Take the body (`size` bytes) and jump to the byte right after it. The next frame header starts there.
4. Stop when the next byte is `00` (padding begins) or the end of the tag is reached.

In the example:
- `TIT2` at `000A`, with a 14-byte body;
- `TBPM` at `0022`, with a 4-byte body;
- `GEOB` at `0030`, with a 47-byte body;
- padding from `0069` to the end.

Because each frame states its own size, **a frame can be skipped without understanding its content**. That is how CrateEdit keeps Serato's GEOB data and Traktor's PRIV data intact: it never interprets them, it copies their bytes.

### Common frames

| Id | Meaning | Body |
|---|---|---|
| `TIT2` `TPE1` `TALB` `TCON` `TPUB` `TBPM` `TKEY` | title, artist, album, genre, label, BPM, key | encoding byte + text |
| `TDRC` (v2.4) / `TYER` (v2.3) | year | encoding byte + text |
| `TXXX` | user-defined text, e.g. Beatport's "label" | encoding + description + value |
| `COMM` | comment | encoding + language (3 bytes, e.g. "eng") + description + text |
| `APIC` | cover art | MIME type, picture type, description, image bytes |
| `UFID` | unique file identifier | owner + identifier |
| `GEOB` | binary object (Serato: cues, beatgrid, overview) | MIME, filename, description, data |
| `PRIV` | private data (Traktor: "TRAKTOR4") | owner + data |

### Text encoding and terminators: `Id3Text`
The first byte of a text body is the **encoding**:

| Byte | Encoding |
|---|---|
| `00` | Latin-1 (Western European, one byte per character) |
| `01` | UTF-16 with a byte-order mark |
| `02` | UTF-16 big-endian (v2.4 only) |
| `03` | UTF-8 (v2.4 only) |

Text may end with a **terminator**: `00`, or `00 00` for UTF-16. Both terminated and unterminated text are common, depending on the writer. In ID3v2.4, a terminator can also separate several values ("DJ Chus" `00` "Harry Romero"); CrateEdit reads those as "DJ Chus, Harry Romero".

### Padding
Zeros after the last frame, up to the declared tag size. Padding is reserved space: an edit that makes frames slightly longer uses it, the tag keeps its size, and **the audio does not have to move**. Without padding, the entire file must be rewritten. CrateEdit also checks that the padding really is all zeros. Anything else means the tag was not fully understood, and it is not written.

## 4. How CrateEdit edits a tag: the example traced

Suppose the BPM is changed from 115 to 116 and a key "Emin" is set. `Mp3TagWriter.Rewrite` does this:

1. **Read the header.** `Mp3TagReader.ReadId3v2` reads 10 bytes. `Id3v2Header.TryParse` checks the `ID3` marker and the synchsafe size: version 4, 119 bytes follow.
2. **Read and parse the tag.** It reads 129 bytes, not the audio. `Id3v2Tag.Parse` walks the frames as described above and returns `[TIT2, TBPM, GEOB]` as raw bytes.
3. **Apply the changes to the frame list.** `Id3FieldMap.Apply` handles each change:
   - **BPM:** maps to `TBPM`, found at index 1. A new body `03 31 31 36` ("116") is built, keeping the old encoding (UTF-8) and the old terminator style (none). It replaces index 1.
   - **Key:** maps to `TKEY`, which does not exist. A new frame `03 45 6D 69 6E` ("Emin") is **appended**.
   - `TIT2` and `GEOB` are not touched. They are the same objects, with the same bytes.
4. **Render.** `Id3v2Tag.Render` writes:
   - the same header;
   - the frames: 110 bytes in total;
   - zeros up to the old size of 119 bytes, because the new frames fit into the padding.

   The result:

   ```
   0020  74 73 54 42 50 4D 00 00 00 04 00 00 03 31 31 36  tsTBPM.......116
   ...
   0060  65 72 73 32 00 01 01 41 51 54 4B 45 59 00 00 00  ers2...AQTKEY...
   0070  05 00 00 03 45 6D 69 6E 00 00 00 00 00 00 00 00  ....Emin........
   ```

   Only `5` became `6`, and `TKEY` took 15 bytes of the padding. The tag is still 129 bytes long, so the audio starts at the same position.
5. **Write the new file.** The new tag is written first. Then the audio is copied from the source in chunks, from the end of the old tag to the start of the ID3v1 tag, without being interpreted. Finally the ID3v1 tag is written, updated if present. The source file is never modified.

If the new frames had not fit (e.g. a much longer comment), the tag would have grown by the missing bytes plus 4 KB of new padding, and the audio would have been written 4 KB further back. It is still copied unchanged.

## 5. What CrateEdit refuses, and why

| Situation | Why it is refused |
|---|---|
| ID3v2.2 | Different frame structure (3-character ids); rare today |
| Tag-level unsynchronisation | The bytes on disk are not the frame bytes; re-encoding errors would corrupt the tag |
| Extended header | May contain a checksum over the frames, which an edit would invalidate |
| Footer | Must be kept consistent with the header; not supported to keep scope small |
| Invalid frame id or size, or non-zero bytes after the last frame | The tag was not fully understood; writing could drop data |
| Edited frame is compressed or encrypted | Its content cannot be read or replaced as plain text |
| Two frames for an edited field (e.g. two `TPE1`) | Unclear which one software reads; editing one would leave the other stale |

Refusing means: the file is not changed, and the reason is reported (`TagWriteException` for now).

## 6. Versions at a glance

| | ID3v2.3 | ID3v2.4 |
|---|---|---|
| Frame size | 32-bit big-endian | synchsafe |
| Encodings | Latin-1, UTF-16 | + UTF-16BE, UTF-8 |
| Year frame | `TYER` | `TDRC` |
| Multiple values | not standardized | separated by terminators |

All of the owner's sample files use ID3v2.4. New tags (for files without one) are created as ID3v2.3, which all major DJ software reads.
