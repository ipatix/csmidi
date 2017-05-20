using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace csmidi
{
    class MidiFile
    {
        public ushort timeDivision;

        public List<MidiTrack> midiTracks { get; private set; }

        public MidiFile()
        {
            midiTracks = new List<MidiTrack>();
            timeDivision = 0;
        }

        public void loadMidiFromFile(string filePath)
        {
            // first clear the currently loaded Midi by clearing the List
            MidiLoader.loadFromFile(filePath, midiTracks, ref timeDivision);      // load the Midi using the MidiLoader class
        }

        public void saveMidiToFile(string filePath)
        {
            MidiSaver.saveToFile(filePath, midiTracks, timeDivision);         // save the Midi to a file
        }

        public void sortTrackEvents()
        {
            for (int currentTrack = 0; currentTrack < midiTracks.Count; currentTrack++)
            {
                // sorts events STABLE
                midiTracks[currentTrack].midiEvents = midiTracks[currentTrack].midiEvents.OrderBy(
                        item => item.absoluteTicks).ToList();
            }
        }
    }

    class MidiTrack
    {
        public List<MidiEvent> midiEvents;

        public MidiTrack()
        {
            midiEvents = new List<MidiEvent>();
        }
    }

    abstract class MidiEvent
    {
        private long _ticks;
        public long absoluteTicks { 
            get { 
                return _ticks;
            } 
            set { 
                if (value < 0)
                    throw new ArgumentOutOfRangeException("A MidiEvent must be at a time >= 0");
                _ticks = value;
            }
        }

        public abstract byte[] getEventData();

        public MidiEvent(long ticks)
        {
            absoluteTicks = ticks;
        }
    }

    class MessageMidiEvent : MidiEvent
    {
        private byte midiChannel;
        private byte parameter1;
        private byte parameter2;
        private NormalType type;

        public override byte[] getEventData()
        {
            byte[] returnData = new byte[3];
            switch (type)
            {
                case NormalType.NoteOFF:                // #0x8
                    returnData[0] = (byte)(midiChannel | (0x8 << 4));
                    returnData[1] = parameter1;     // note number
                    returnData[2] = parameter2;     // velocity
                    break;
                case NormalType.NoteON:                 // #0x9
                    returnData[0] = (byte)(midiChannel | (0x9 << 4));
                    returnData[1] = parameter1;     // note number
                    returnData[2] = parameter2;     // velocity
                    break;
                case NormalType.NoteAftertouch:         // #0xA
                    returnData[0] = (byte)(midiChannel | (0xA << 4));
                    returnData[1] = parameter1;     // note number
                    returnData[2] = parameter2;     // aftertouch value
                    break;
                case NormalType.Controller:             // #0xB
                    returnData[0] = (byte)(midiChannel | (0xB << 4));
                    returnData[1] = parameter1;     // controller number
                    returnData[2] = parameter2;     // controller value
                    break;
                case NormalType.Program:                // #0xC
                    returnData = new byte[2];           // this event doesn't have a 2nd parameter
                    returnData[0] = (byte)(midiChannel | (0xC << 4));
                    returnData[1] = parameter1;     // program number
                    break;
                case NormalType.ChannelAftertouch:      // #0xD
                    returnData = new byte[2];           // this event doesn't have a 2nd parameter
                    returnData[0] = (byte)(midiChannel | (0xD << 4));
                    returnData[1] = parameter1;     // aftertouch value
                    break;
                case NormalType.PitchBend:              // #0xE
                    returnData[0] = (byte)(midiChannel | (0xE << 4));
                    returnData[1] = parameter1;     // pitch LSB
                    returnData[2] = parameter2;     // pitch MSB
                    break;
            }
            return returnData;
        }

        public MessageMidiEvent(long tick, byte midiChannel, NormalType type, byte par1, byte par2)
            : base(tick)
        {
            this.midiChannel = midiChannel;
            this.type = type;
            this.parameter1 = par1;
            this.parameter2 = par2;
        }
    }

    class MetaMidiEvent : MidiEvent
    {
        private byte[] data;
        private byte metaType;

        public override byte[] getEventData()     // returns a raw byte array of this META Event in the MIDI file
        {
            byte[] dataLength = VariableLength.ConvertToVariableLength(data.Length);
            byte[] returnData = new byte[data.Length + 2 + dataLength.Length];
            returnData[0] = 0xFF;
            returnData[1] = metaType;
            Array.Copy(dataLength, 0, returnData, 2, dataLength.Length);
            Array.Copy(data, 0, returnData, 2 + dataLength.Length, data.Length);
            return returnData;
        }

        public MetaMidiEvent(long tick, byte _metaType, byte[] _data)
            : base(tick)
        {
            metaType = _metaType;
            data = _data;
        }
    }

    class SysExMidiEvent : MidiEvent
    {
        private byte[] data;
        private byte sysexType;

        public override byte[] getEventData()     // returns a raw byte array of this SysEx Event in the MIDI file
        {
            byte[] dataLength = VariableLength.ConvertToVariableLength(data.Length);
            byte[] returnData = new byte[data.Length + 1 + dataLength.Length];
            returnData[0] = sysexType;
            Array.Copy(dataLength, 0, returnData, 1, dataLength.Length);
            Array.Copy(data, 0, returnData, 1 + dataLength.Length, data.Length);
            return returnData;
        }

        public SysExMidiEvent(long tick, byte sysexType, byte[] data)
            : base(tick)
        {
            this.sysexType = sysexType;
            this.data = data;
        }
    }

    internal enum NormalType
    {
        NoteON, 
        NoteOFF, 
        NoteAftertouch, 
        Controller, 
        Program, 
        ChannelAftertouch, 
        PitchBend
    }

    internal static class VariableLength
    {
        internal static byte[] ConvertToVariableLength(long value)
        {
            int i = 0;
            byte[] returnData = new byte[i + 1];
            returnData[i] = (byte)(value & 0x7F);
            i++;

            value = value >> 7;

            while (value != 0)
            {
                Array.Resize(ref returnData, i + 1);
                returnData[i] = (byte)((value & 0x7F) | 0x80);
                value = value >> 7;
                i++;
            }

            Array.Reverse(returnData);
            return returnData;
        }

        internal static long ConvertToLong(byte[] values)
        {
            long value = 0;
            for (int i = 0; i < values.Length; i++)
            {
                value = value << 7;     // doesn't matter on first loop anyway, if it's one of the next loops it shifts the latest value up 7 bits
                value = value | (byte)(values[i] & 0x7F);
            }
            return value;
        }
    }
}
