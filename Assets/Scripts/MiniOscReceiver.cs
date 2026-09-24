using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Receiver OSC/UDP toi gian, tu viet (khong dung plugin ngoai): chi doc dia chi va cac
/// tham so kieu int/float/string can dung cho pipeline theo doi khach tham quan.
/// Khong ho tro OSC bundle (#bundle) - goi tin bundle se bi bo qua.
/// </summary>
public class MiniOscReceiver : MonoBehaviour
{
    [Tooltip("Cong UDP lang nghe OSC.")]
    public int port = 12000;

    public event Action<string, object[]> OnOscMessage;

    public bool PortBound { get; private set; }
    public float SecondsSinceLastMessage => _lastMessageTime < 0f ? -1f : Time.time - _lastMessageTime;

    UdpClient _client;
    Thread _thread;
    volatile bool _running;
    readonly ConcurrentQueue<(string address, object[] args)> _queue = new();
    float _lastMessageTime = -1f;

    void OnEnable()
    {
        try
        {
            _client = new UdpClient(port);
            _running = true;
            _thread = new Thread(ReceiveLoop) { IsBackground = true };
            _thread.Start();
            PortBound = true;
            Debug.Log($"[MiniOscReceiver] Dang lang nghe OSC tren port {port}");
        }
        catch (Exception e)
        {
            PortBound = false;
            Debug.LogError($"[MiniOscReceiver] Khong the mo port {port}: {e.Message}");
        }
    }

    void OnDisable()
    {
        _running = false;
        PortBound = false;
        _client?.Close(); // ngat Receive() dang block trong thread nen
        _thread?.Join(200);
        _client = null;
        _thread = null;
    }

    void ReceiveLoop()
    {
        var remote = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            byte[] data;
            try { data = _client.Receive(ref remote); }
            catch { break; } // socket bi dong (OnDisable) hoac loi mang

            if (TryParseMessage(data, out string address, out object[] args))
                _queue.Enqueue((address, args));
        }
    }

    void Update()
    {
        // Chuyen du lieu tu thread nen sang main thread truoc khi ban ra ngoai
        while (_queue.TryDequeue(out var msg))
        {
            _lastMessageTime = Time.time;
            OnOscMessage?.Invoke(msg.address, msg.args);
        }
    }

    internal static bool TryParseMessage(byte[] data, out string address, out object[] args)
    {
        address = null;
        args = null;
        int offset = 0;

        if (!TryReadOscString(data, ref offset, out address)) return false;
        if (address.Length == 0 || address[0] != '/') return false; // bo qua "#bundle" va goi tin loi

        if (!TryReadOscString(data, ref offset, out string typeTags) ||
            typeTags.Length == 0 || typeTags[0] != ',')
            return false;

        var result = new object[typeTags.Length - 1];
        for (int i = 1; i < typeTags.Length; i++)
        {
            switch (typeTags[i])
            {
                case 'i':
                    if (offset + 4 > data.Length) return false;
                    result[i - 1] = ReadInt32BE(data, offset);
                    offset += 4;
                    break;
                case 'f':
                    if (offset + 4 > data.Length) return false;
                    result[i - 1] = ReadFloatBE(data, offset);
                    offset += 4;
                    break;
                case 's':
                    if (!TryReadOscString(data, ref offset, out string s)) return false;
                    result[i - 1] = s;
                    break;
                default:
                    return false; // kieu chua ho tro (blob, bool, ...)
            }
        }

        args = result;
        return true;
    }

    static bool TryReadOscString(byte[] data, ref int offset, out string value)
    {
        value = null;
        int start = offset;
        while (offset < data.Length && data[offset] != 0) offset++;
        if (offset >= data.Length) return false;
        value = Encoding.ASCII.GetString(data, start, offset - start);
        offset++; // bo qua byte 0 ket thuc chuoi
        offset = (offset + 3) & ~3; // can le ve boi so cua 4 theo dung OSC spec
        return true;
    }

    static int ReadInt32BE(byte[] d, int o) =>
        (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];

    static float ReadFloatBE(byte[] d, int o)
    {
        if (!BitConverter.IsLittleEndian) return BitConverter.ToSingle(d, o);
        byte[] tmp = { d[o + 3], d[o + 2], d[o + 1], d[o] };
        return BitConverter.ToSingle(tmp, 0);
    }

#if UNITY_EDITOR
    // Self-test toi thieu cho parser OSC - chay khi Editor load script, khong can framework test.
    [UnityEditor.InitializeOnLoadMethod]
    static void SelfTestParser()
    {
        byte[] packet = BuildTestMessage("/person/x", 0.5f);
        bool ok = TryParseMessage(packet, out string addr, out object[] args)
            && addr == "/person/x" && args.Length == 1 && Mathf.Approximately((float)args[0], 0.5f);
        Debug.Assert(ok, "[MiniOscReceiver] Self-test parser OSC that bai");
    }

    static byte[] BuildTestMessage(string address, float value)
    {
        using var ms = new System.IO.MemoryStream();
        WriteOscString(ms, address);
        WriteOscString(ms, ",f");
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        ms.Write(bytes, 0, 4);
        return ms.ToArray();
    }

    static void WriteOscString(System.IO.MemoryStream ms, string s)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(s);
        ms.Write(bytes, 0, bytes.Length);
        int pad = 4 - (bytes.Length % 4);
        for (int i = 0; i < pad; i++) ms.WriteByte(0);
    }
#endif
}
