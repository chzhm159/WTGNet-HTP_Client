﻿using log4net;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WTGNet_Client.utils
{
    internal class ModbusTCPHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ModbusTCPHelper));
        private EasyModbus.ModbusClient modbusClient;
        private string modbus_Ip="127.0.0.1";
        private int modbus_Port=502;
        public void Init() {
            modbusClient = new EasyModbus.ModbusClient();
            modbusClient.ReceiveDataChanged += new EasyModbus.ModbusClient.ReceiveDataChangedHandler(UpdateReceiveData);
            modbusClient.SendDataChanged += new EasyModbus.ModbusClient.SendDataChangedHandler(UpdateSendData);
            modbusClient.ConnectedChanged += new EasyModbus.ModbusClient.ConnectedChangedHandler(UpdateConnectedChanged);
        }
        public void Connect() {
            try {
                Init();
                if (modbusClient.Connected)
                    modbusClient.Disconnect();
                modbusClient.IPAddress = modbus_Ip;
                modbusClient.Port = modbus_Port;
                modbusClient.SerialPort = null;
                modbusClient.Connect();
                log.InfoFormat("链接成功");
                //if (cbbSelctionModbus.SelectedIndex == 0) {



                //    //modbusClient.receiveDataChanged += new EasyModbus.ModbusClient.ReceiveDataChanged(UpdateReceiveData);
                //    //modbusClient.sendDataChanged += new EasyModbus.ModbusClient.SendDataChanged(UpdateSendData);
                //    //modbusClient.connectedChanged += new EasyModbus.ModbusClient.ConnectedChanged(UpdateConnectedChanged);


                //}

            } catch (Exception exc) {
                log.InfoFormat("链接异常:{0},{1}",exc.Message,exc.StackTrace);
                MessageBox.Show(exc.Message, "Unable to connect to Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        string receiveData = null;

        void UpdateReceiveData(object sender) {
            receiveData = "Rx: " + BitConverter.ToString(modbusClient.receiveData).Replace("-", " ") + System.Environment.NewLine;
            Thread thread = new Thread(updateReceiveTextBox);
            thread.Start();
        }
        delegate void UpdateReceiveDataCallback();
        void updateReceiveTextBox() {
            //if (textBox1.InvokeRequired) {
            //    UpdateReceiveDataCallback d = new UpdateReceiveDataCallback(updateReceiveTextBox);
            //    this.Invoke(d, new object[] { });
            //} else {
            //    textBox1.AppendText(receiveData);
            //}
        }

        string sendData = null;
        void UpdateSendData(object sender) {
            sendData = "Tx: " + BitConverter.ToString(modbusClient.sendData).Replace("-", " ") + System.Environment.NewLine;
            Thread thread = new Thread(updateSendTextBox);
            thread.Start();

        }
        void updateSendTextBox() {
            //if (textBox1.InvokeRequired) {
            //    UpdateReceiveDataCallback d = new UpdateReceiveDataCallback(updateSendTextBox);
            //    this.Invoke(d, new object[] { });
            //} else {
            //    textBox1.AppendText(sendData);
            //}
        }
        private void UpdateConnectedChanged(object sender) {
            if (modbusClient.Connected) {
                log.InfoFormat("链接以建立 !!");
                //    txtConnectedStatus.Text = "Connected to Server";
                //    txtConnectedStatus.BackColor = Color.Green;
            } else {
                log.InfoFormat("未能链接到服务端");
                //    txtConnectedStatus.Text = "Not Connected to Server";
                //    txtConnectedStatus.BackColor = Color.Red;
            }
        }
    }
}
