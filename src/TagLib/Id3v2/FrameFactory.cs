/***************************************************************************
    copyright            : (C) 2005 by Brian Nickel
    email                : brian.nickel@gmail.com
    based on             : id3v2framefactory.cpp from TagLib
 ***************************************************************************/

/***************************************************************************
 *   This library is free software; you can redistribute it and/or modify  *
 *   it  under the terms of the GNU Lesser General Public License version  *
 *   2.1 as published by the Free Software Foundation.                     *
 *                                                                         *
 *   This library is distributed in the hope that it will be useful, but   *
 *   WITHOUT ANY WARRANTY; without even the implied warranty of            *
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU     *
 *   Lesser General Public License for more details.                       *
 *                                                                         *
 *   You should have received a copy of the GNU Lesser General Public      *
 *   License along with this library; if not, write to the Free Software   *
 *   Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  *
 *   USA                                                                   *
 ***************************************************************************/

using System.Collections;
 
namespace TagLib.Id3v2
{
   public class FrameFactory
   {
      public delegate Frame FrameCreator (ByteVector data, FrameHeader header);

      //////////////////////////////////////////////////////////////////////////
      // private properties
      //////////////////////////////////////////////////////////////////////////
      private static StringType default_encoding     = StringType.UTF8;
      private static bool       use_default_encoding = false;
      private static ArrayList  frame_creators = new ArrayList ();
      
      //////////////////////////////////////////////////////////////////////////
      // public members
      //////////////////////////////////////////////////////////////////////////
      public static Frame CreateFrame (ByteVector data, uint version)
      {
         FrameHeader header = new FrameHeader (data, version);
         ByteVector frame_id = header.FrameId;
         // A quick sanity check -- make sure that the frame_id is 4 uppercase
         // Latin1 characters.  Also make sure that there is data in the frame.
         
         if(frame_id == null || frame_id.Count != (version < 3 ? 3 : 4) || header.FrameSize < 0)
            return null;
         
         foreach (byte b in frame_id)
         {
            char c = (char) b;
            if ((c < 'A' || c > 'Z') && (c < '1' || c > '9'))
               return null;
         }
         
         // Windows Media Player may create zero byte frames. Just send them
         // off as unknown.
         if (header.FrameSize == 0)
            return new UnknownFrame (data, header);
         
         // TagLib doesn't mess with encrypted frames, so just treat them
         // as unknown frames.

         if (header.Compression)
         {
            Debugger.Debug ("Compressed frames are currently not supported.");
            return new UnknownFrame(data, header);
         }
         
         if (header.Encryption)
         {
            Debugger.Debug ("Encrypted frames are currently not supported.");
            return new UnknownFrame(data, header);
         }

         if(!UpdateFrame (header))
         {
            header.TagAlterPreservation = true;
            return new UnknownFrame (data, header);
         }
         
         foreach (FrameCreator creator in frame_creators)
         {
            Frame frame = creator (data, header);
            if (frame != null)
               return frame;
         }
         
         
         // UpdateFrame() might have updated the frame ID.

         frame_id = header.FrameId;
         
         // This is where things get necissarily nasty.  Here we determine which
         // Frame subclass (or if none is found simply an Frame) based
         // on the frame ID.  Since there are a lot of possibilities, that means
         // a lot of if blocks.
         
         // Text Identification (frames 4.2)
         
         if(frame_id.StartsWith ("T"))
         {
            TextIdentificationFrame f = frame_id != "TXXX"
            ? new TextIdentificationFrame (data, header)
            : new UserTextIdentificationFrame (data, header);
            
            if(use_default_encoding)
               f.TextEncoding = default_encoding;
            
            return f;
         }

         // Comments (frames 4.10)

         if (frame_id == "COMM")
         {
            CommentsFrame f = new CommentsFrame (data, header);
            
            if(use_default_encoding)
               f.TextEncoding = default_encoding;
            
            return f;
         }

         // Attached Picture (frames 4.14)

         if (frame_id == "APIC")
         {
            AttachedPictureFrame f = new AttachedPictureFrame (data, header);
            
            if(use_default_encoding)
               f.TextEncoding = default_encoding;
            
            return f;
         }

         // Relative Volume Adjustment (frames 4.11)

         if (frame_id == "RVA2")
            return new RelativeVolumeFrame (data, header);

         // Unique File Identifier (frames 4.1)

         if (frame_id == "UFID")
            return new UniqueFileIdentifierFrame (data, header);

         // Private (frames 4.27)

         if (frame_id == "PRIV")
            return new PrivateFrame (data, header);
         
         return new UnknownFrame(data, header);
      }

      public static StringType DefaultTextEncoding
      {
         get
         {
            return default_encoding;
         }
         set
         {
            use_default_encoding = true;
            default_encoding = value;
         }
      }
      
      public static void AddFrameCreator (FrameCreator creator)
      {
         if (creator != null)
            frame_creators.Insert (0, creator);
      }
      
      
      //////////////////////////////////////////////////////////////////////////
      // private members
      //////////////////////////////////////////////////////////////////////////
      private FrameFactory () {}
      
      private static bool UpdateFrame (FrameHeader header)
      {
         ByteVector frame_id = header.FrameId;

         switch (header.Version)
         {
            case 2: // ID3v2.2
            {
               if(frame_id == "CRM" ||
                  frame_id == "EQU" ||
                  frame_id == "LNK" ||
                  frame_id == "RVA" ||
                  frame_id == "TIM" ||
                  frame_id == "TSI")
               {
                  Debugger.Debug ("ID3v2.4 no longer supports the frame type "
                  + frame_id.ToString () + ".  It will be discarded from the tag.");
                  
                  return false;
               }

               // ID3v2.2 only used 3 bytes for the frame ID, so we need to convert all of
               // the frames to their 4 byte ID3v2.4 equivalent.

               ConvertFrame ("BUF", "RBUF", header);
               ConvertFrame ("CNT", "PCNT", header);
               ConvertFrame ("COM", "COMM", header);
               ConvertFrame ("CRA", "AENC", header);
               ConvertFrame ("ETC", "ETCO", header);
               ConvertFrame ("GEO", "GEOB", header);
               ConvertFrame ("IPL", "TIPL", header);
               ConvertFrame ("MCI", "MCDI", header);
               ConvertFrame ("MLL", "MLLT", header);
               ConvertFrame ("PIC", "APIC", header);
               ConvertFrame ("POP", "POPM", header);
               ConvertFrame ("REV", "RVRB", header);
               ConvertFrame ("SLT", "SYLT", header);
               ConvertFrame ("STC", "SYTC", header);
               ConvertFrame ("TAL", "TALB", header);
               ConvertFrame ("TBP", "TBPM", header);
               ConvertFrame ("TCM", "TCOM", header);
               ConvertFrame ("TCO", "TCON", header);
               ConvertFrame ("TCR", "TCOP", header);
               ConvertFrame ("TDA", "TDRC", header);
               ConvertFrame ("TDY", "TDLY", header);
               ConvertFrame ("TEN", "TENC", header);
               ConvertFrame ("TFT", "TFLT", header);
               ConvertFrame ("TKE", "TKEY", header);
               ConvertFrame ("TLA", "TLAN", header);
               ConvertFrame ("TLE", "TLEN", header);
               ConvertFrame ("TMT", "TMED", header);
               ConvertFrame ("TOA", "TOAL", header);
               ConvertFrame ("TOF", "TOFN", header);
               ConvertFrame ("TOL", "TOLY", header);
               ConvertFrame ("TOR", "TDOR", header);
               ConvertFrame ("TOT", "TOAL", header);
               ConvertFrame ("TP1", "TPE1", header);
               ConvertFrame ("TP2", "TPE2", header);
               ConvertFrame ("TP3", "TPE3", header);
               ConvertFrame ("TP4", "TPE4", header);
               ConvertFrame ("TPA", "TPOS", header);
               ConvertFrame ("TPB", "TPUB", header);
               ConvertFrame ("TRC", "TSRC", header);
               ConvertFrame ("TRD", "TDRC", header);
               ConvertFrame ("TRK", "TRCK", header);
               ConvertFrame ("TSS", "TSSE", header);
               ConvertFrame ("TT1", "TIT1", header);
               ConvertFrame ("TT2", "TIT2", header);
               ConvertFrame ("TT3", "TIT3", header);
               ConvertFrame ("TXT", "TOLY", header);
               ConvertFrame ("TXX", "TXXX", header);
               ConvertFrame ("TYE", "TDRC", header);
               ConvertFrame ("UFI", "UFID", header);
               ConvertFrame ("ULT", "USLT", header);
               ConvertFrame ("WAF", "WOAF", header);
               ConvertFrame ("WAR", "WOAR", header);
               ConvertFrame ("WAS", "WOAS", header);
               ConvertFrame ("WCM", "WCOM", header);
               ConvertFrame ("WCP", "WCOP", header);
               ConvertFrame ("WPB", "WPUB", header);
               ConvertFrame ("WXX", "WXXX", header);

            }
            break;

            case 3: // ID3v2.3
            {
               if(frame_id == "EQUA" ||
                  frame_id == "RVAD" ||
                  frame_id == "TIME" ||
                  frame_id == "TRDA" ||
                  frame_id == "TSIZ" ||
                  frame_id == "TDAT")
               {
                  Debugger.Debug ("ID3v2.4 no longer supports the frame type "
                  + frame_id.ToString () + ".  It will be discarded from the tag.");
                  
                  return false;
               }

               ConvertFrame ("TORY", "TDOR", header);
               ConvertFrame ("TYER", "TDRC", header);

            }
            break;

            default:
            {
               // This should catch a typo that existed in TagLib up to and including
               // version 1.1 where TRDC was used for the year rather than TDRC.

               ConvertFrame ("TRDC", "TDRC", header);
            }
            break;
         }

         return true;
      }

      private static void ConvertFrame (string from, string to, FrameHeader header)
      {
         if (header.FrameId != from)
            return;
         
         header.FrameId = to;
      }
   }
}