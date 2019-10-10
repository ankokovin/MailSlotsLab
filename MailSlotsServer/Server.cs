using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.InteropServices;

namespace MailSlots
{
    public partial class frmMain : Form
    {
        private Dictionary<string, string> Nicknames = new Dictionary<string, string>();


        private int ClientHandleMailSlot;       // дескриптор мэйлслота
        private string MailSlotName = "\\\\" + Dns.GetHostName() + "\\mailslot\\ServerMailslot";    // имя мэйлслота, Dns.GetHostName() - метод, возвращающий имя машины, на которой запущено приложение
        private Thread t;                       // поток для обслуживания мэйлслота
        private bool _continue = true;          // флаг, указывающий продолжается ли работа с мэйлслотом

        // конструктор формы
        public frmMain()
        {
            InitializeComponent();

            // создание мэйлслота
            ClientHandleMailSlot = DIS.Import.CreateMailslot("\\\\.\\mailslot\\ServerMailslot", 0, DIS.Types.MAILSLOT_WAIT_FOREVER, 0);

            // вывод имени мэйлслота в заголовок формы, чтобы можно было его использовать для ввода имени в форме клиента, запущенного на другом вычислительном узле
            this.Text += "     " + MailSlotName;

            // создание потока, отвечающего за работу с мэйлслотом
            Thread t = new Thread(ReceiveMessage);
            t.Start();
        }

        private int GetSourceIdx(string Message)
        {
            return Message.IndexOf(">>") - 1;
        }

        private void ReceiveMessage()
        {
            string reseviedMessage = "";            // прочитанное сообщение
            int MailslotSize = 0;       // максимальный размер сообщения
            int lpNextSize = 0;         // размер следующего сообщения
            int MessageCount = 0;       // количество сообщений в мэйлслоте
            uint realBytesReaded = 0;   // количество реально прочитанных из мэйлслота байтов

            // входим в бесконечный цикл работы с мэйлслотом
            while (_continue)
            {
                // получаем информацию о состоянии мэйлслота
                DIS.Import.GetMailslotInfo(ClientHandleMailSlot, MailslotSize, ref lpNextSize, ref MessageCount, 0);

                // если есть сообщения в мэйлслоте, то обрабатываем каждое из них
                if (MessageCount > 0)
                    for (int i = 0; i < MessageCount; i++)
                    {
                        byte[] buff = new byte[1024];                           // буфер прочитанных из мэйлслота байтов
                        DIS.Import.FlushFileBuffers(ClientHandleMailSlot);      // "принудительная" запись данных, расположенные в буфере операционной системы, в файл мэйлслота
                        DIS.Import.ReadFile(ClientHandleMailSlot, buff, 1024, ref realBytesReaded, 0);      // считываем последовательность байтов из мэйлслота в буфер buff
                        reseviedMessage = Encoding.Unicode.GetString(buff);                 // выполняем преобразование байтов в последовательность символов
                        int sourceidx = GetSourceIdx(reseviedMessage);
                        string source = reseviedMessage.Substring(0, sourceidx);
                        string resultMessage = "";
                        string content = reseviedMessage.Substring(sourceidx+3, reseviedMessage.IndexOf('\0')- (sourceidx + 3));
                        if (Nicknames.ContainsKey(source))
                        {
                            string nickname = Nicknames[source];
                            resultMessage =  nickname+":" + content;
                        }
                        else
                        {
                            Nicknames.Add(source, content);
                            resultMessage = "["+ source +"]Новый пользователь: " + content;
                        }
                        rtbMessages.Invoke((MethodInvoker)delegate
                        {
                            if (resultMessage != "")
                                rtbMessages.Text += " >> " + resultMessage + " \n";     // выводим полученное сообщение на форму
                        });
                        Thread.Sleep(500);                                      // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
                    }
            }
        }

       

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _continue = false;      // сообщаем, что работа с мэйлслотом завершена

            if (t != null)
                t.Abort();          // завершаем поток

            if (ClientHandleMailSlot != -1)
                DIS.Import.CloseHandle(ClientHandleMailSlot);            // закрываем дескриптор мэйлслота
        }
    }
}