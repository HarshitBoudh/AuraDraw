using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class UDPFingerReceiver : MonoBehaviour
{
    public int port = 5005;

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning = true;

    private Vector2 fingerPos = Vector2.zero;
    public Vector2 FingerPos => fingerPos;

    private bool isPinching = false;
    public bool IsPinching => isPinching;

    private float pressure = 1f;
    public float Pressure => pressure;

    private void Start()
    {
        isRunning = true;
        udpClient = new UdpClient(port);
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log("✅ UDPFingerReceiver Started on port " + port);
    }

    private void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, port);
        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEP);

                if (data.Length == 12 || data.Length == 16)
                {
                    float x        = BitConverter.ToSingle(data, 0);
                    float y        = BitConverter.ToSingle(data, 4);
                    float pinchVal = BitConverter.ToSingle(data, 8);
                    float pressVal = (data.Length == 16) ? BitConverter.ToSingle(data, 12) : 1f;

                    lock (this)
                    {
                        fingerPos.x = Mathf.Clamp01(x);
                        fingerPos.y = Mathf.Clamp01(y);
                        isPinching  = (pinchVal > 0.5f);

                        // ✅ FIXED: was Clamp01 which capped at 1.0
                        // Python sends 0.4–3.0, so we allow up to 5.0 for headroom
                        pressure = Mathf.Clamp(pressVal, 0f, 5f);
                    }
                }
            }
            catch (Exception e)
            {
                if (isRunning)
                    Debug.LogWarning("UDP Receive Error: " + e.Message);
            }
        }
    }

    private void OnApplicationQuit()
    {
        isRunning = false;
        if (receiveThread != null && receiveThread.IsAlive)
            receiveThread.Join(100);
        if (udpClient != null)
            udpClient.Close();
    }

    private void OnDisable()
    {
        isRunning = false;
        if (udpClient != null) udpClient.Close();
    }
}