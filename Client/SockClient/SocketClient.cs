﻿/*
 * 北风之神SOCKET框架(ZYSocket)
 *  Borey Socket Frame(ZYSocket)
 *  by luyikk@126.com
 *  Updated 2010-12-26 
 */
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using ZYNet.CloudSystem.Client;

namespace ZYNet.CloudSystem.SocketClient
{

    public delegate void ConnectionOk(string message,bool IsConn);
    public delegate void DataOn(byte[] Data);
    public delegate void ExceptionDisconnection(string message);

    /// <summary>
    /// ZYSOCKET 客户端
    /// （一个简单的异步SOCKET客户端，性能不错。支持.NET 3.0以上版本。适用于silverlight)
    /// </summary>
    public class SocketClient:ISyncClient
    {
        /// <summary>
        /// SOCKET对象
        /// </summary>
        public Socket sock { get; private set; }

        /// <summary>
        /// 连接成功事件
        /// </summary>
        public event ConnectionOk Connection;

        /// <summary>
        /// 数据包进入事件
        /// </summary>
        public event Action<byte[]> BinaryInput;
        /// <summary>
        /// 出错或断开触发事件
        /// </summary>
        public event Action<string> Disconnect;

        private System.Threading.AutoResetEvent wait = new System.Threading.AutoResetEvent(false);

        private AsyncSend _SendObj;
        
        public SocketClient()
        {           
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _SendObj = new AsyncSend(sock);
        }

        private bool IsConn;

        public SocketAsyncEventArgs AsynEvent { get; private set; }

        /// <summary>
        /// 异步连接到指定的服务器
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        public void BeginConnectionTo(string host, int port)
        {
            IPEndPoint myEnd = null;

            #region ipformat
            try
            {
                myEnd = new IPEndPoint(IPAddress.Parse(host), port);
            }
            catch (FormatException)
            {
#if!COREFX
                IPHostEntry p = Dns.GetHostEntry(Dns.GetHostName());
#else
                IPHostEntry p = Dns.GetHostEntryAsync(Dns.GetHostName()).Result;
#endif

                foreach (IPAddress s in p.AddressList)
                {
                    if (!s.IsIPv6LinkLocal)
                        myEnd = new IPEndPoint(s, port);
                }
            }

            #endregion

            SocketAsyncEventArgs e = new SocketAsyncEventArgs();

            e.RemoteEndPoint = myEnd;
            e.Completed += new EventHandler<SocketAsyncEventArgs>(e_Completed);

           
            if (!sock.ConnectAsync(e))
            {
                eCompleted(e);
            }
        }

        public bool Connect(string host, int port)
        {
            IPEndPoint myEnd = null;

            #region ipformat
            try
            {
                myEnd = new IPEndPoint(IPAddress.Parse(host), port);
            }
            catch (FormatException)
            {
#if !COREFX
                IPHostEntry p = Dns.GetHostEntry(Dns.GetHostName());
#else
                IPHostEntry p = Dns.GetHostEntryAsync(Dns.GetHostName()).Result;
#endif
                foreach (IPAddress s in p.AddressList)
                {
                    if (!s.IsIPv6LinkLocal)
                        myEnd = new IPEndPoint(s, port);
                }
            }

            #endregion

            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.RemoteEndPoint = myEnd;
            e.Completed += new EventHandler<SocketAsyncEventArgs>(e_Completed);
            if (!sock.ConnectAsync(e))
            {
                eCompleted(e);
            }

            wait.WaitOne();
            wait.Reset();

            return IsConn;
        }



        void e_Completed(object sender, SocketAsyncEventArgs e)
        {
            eCompleted(e);
        }


        void eCompleted(SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Connect:

                    if (e.SocketError == SocketError.Success)
                    {

                        IsConn = true;
                        wait.Set();

                        if (Connection != null)
                            Connection("连接成功", true);

                        byte[] data = new byte[4096];
                        e.SetBuffer(data, 0, data.Length);  //设置数据包

                        try
                        {
                            if (!sock.ReceiveAsync(e)) //开始读取数据包
                                eCompleted(e);
                        }
                        catch (ObjectDisposedException)
                        {
                            if (Disconnect != null)
                                Disconnect("与服务器断开连接");
                        }

                    }
                    else
                    {
                        IsConn = false;
                        wait.Set();
                        if (Connection != null)
                            Connection("连接失败", false);
                    }
                    break;

                case SocketAsyncOperation.Receive:
                    if (e.SocketError == SocketError.Success && e.BytesTransferred > 0)
                    {
                        byte[] data = new byte[e.BytesTransferred];
                        Buffer.BlockCopy(e.Buffer, 0, data, 0, data.Length);


                        //byte[] dataLast = new byte[4098];
                        //e.SetBuffer(dataLast, 0, dataLast.Length);

                        if (BinaryInput != null)
                            BinaryInput(data);

                        try
                        {
                            if (!sock.ReceiveAsync(e))
                                eCompleted(e);
                        }
                        catch (ObjectDisposedException)
                        {
                            if (Disconnect != null)
                                Disconnect("与服务器断开连接");
                        }

                    }
                    else
                    {
                        if (Disconnect != null)
                            Disconnect("与服务器断开连接");
                    }
                    break;

            }
        }


     
        /// <summary>
        /// 发送数据包
        /// </summary>
        /// <param name="data"></param>
        public virtual void SendTo(byte[] data)
        {
            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.SetBuffer(data, 0, data.Length);
            sock.SendAsync(e);
        }

        public virtual void Send(byte[] data)
        {
            try
            {
                _SendObj.Send(data);

            }
            catch (ObjectDisposedException)
            {
                if (Disconnect != null)
                    Disconnect("与服务器断开连接");
            }
            catch (SocketException)
            {
                try
                {
#if !COREFX
                    sock.Close();
#endif
                    sock.Dispose();
                }
                catch { }

                if (Disconnect != null)
                    Disconnect("与服务器断开连接");
            }          

            
        }



        public void Close()
        {
            try
            {
                sock.Shutdown(SocketShutdown.Both);

#if !COREFX
                sock.Disconnect(false);
                sock.Close();
                wait.Close();
#endif
                sock.Dispose();
                wait.Dispose();

            }
            catch (ObjectDisposedException)
            {
            }
            catch (NullReferenceException)
            {

            }
        }
    }


    public interface ISend
    {
        bool Send(byte[] data);
    }
}
