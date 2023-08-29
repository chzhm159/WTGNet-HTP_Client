namespace WTGNet_Client
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent() {
            this.app_btn_connect = new System.Windows.Forms.Button();
            this.app_pn_top = new System.Windows.Forms.Panel();
            this.app_pn_bottom = new System.Windows.Forms.Panel();
            this.app_pn_center = new System.Windows.Forms.Panel();
            this.console_1 = new System.Windows.Forms.RichTextBox();
            this.app_btn_read = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.app_tl_states = new System.Windows.Forms.TableLayoutPanel();
            this.app_lb_state1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.textBox3 = new System.Windows.Forms.TextBox();
            this.textBox4 = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.app_pn_top.SuspendLayout();
            this.app_pn_bottom.SuspendLayout();
            this.app_pn_center.SuspendLayout();
            this.app_tl_states.SuspendLayout();
            this.SuspendLayout();
            // 
            // app_btn_connect
            // 
            this.app_btn_connect.Location = new System.Drawing.Point(370, 64);
            this.app_btn_connect.Name = "app_btn_connect";
            this.app_btn_connect.Size = new System.Drawing.Size(75, 30);
            this.app_btn_connect.TabIndex = 0;
            this.app_btn_connect.Text = "链接";
            this.app_btn_connect.UseVisualStyleBackColor = true;
            this.app_btn_connect.Click += new System.EventHandler(this.button1_Click);
            // 
            // app_pn_top
            // 
            this.app_pn_top.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.app_pn_top.Controls.Add(this.app_tl_states);
            this.app_pn_top.Dock = System.Windows.Forms.DockStyle.Top;
            this.app_pn_top.Location = new System.Drawing.Point(0, 0);
            this.app_pn_top.Name = "app_pn_top";
            this.app_pn_top.Padding = new System.Windows.Forms.Padding(3);
            this.app_pn_top.Size = new System.Drawing.Size(789, 54);
            this.app_pn_top.TabIndex = 1;
            // 
            // app_pn_bottom
            // 
            this.app_pn_bottom.Controls.Add(this.console_1);
            this.app_pn_bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.app_pn_bottom.Location = new System.Drawing.Point(0, 377);
            this.app_pn_bottom.Name = "app_pn_bottom";
            this.app_pn_bottom.Padding = new System.Windows.Forms.Padding(3);
            this.app_pn_bottom.Size = new System.Drawing.Size(789, 212);
            this.app_pn_bottom.TabIndex = 2;
            // 
            // app_pn_center
            // 
            this.app_pn_center.Controls.Add(this.textBox2);
            this.app_pn_center.Controls.Add(this.textBox3);
            this.app_pn_center.Controls.Add(this.textBox4);
            this.app_pn_center.Controls.Add(this.textBox1);
            this.app_pn_center.Controls.Add(this.app_btn_connect);
            this.app_pn_center.Controls.Add(this.label3);
            this.app_pn_center.Controls.Add(this.label2);
            this.app_pn_center.Controls.Add(this.label4);
            this.app_pn_center.Controls.Add(this.label1);
            this.app_pn_center.Controls.Add(this.app_btn_read);
            this.app_pn_center.Dock = System.Windows.Forms.DockStyle.Fill;
            this.app_pn_center.Location = new System.Drawing.Point(0, 54);
            this.app_pn_center.Name = "app_pn_center";
            this.app_pn_center.Size = new System.Drawing.Size(789, 323);
            this.app_pn_center.TabIndex = 3;
            // 
            // console_1
            // 
            this.console_1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.console_1.Location = new System.Drawing.Point(3, 3);
            this.console_1.Name = "console_1";
            this.console_1.Size = new System.Drawing.Size(783, 206);
            this.console_1.TabIndex = 0;
            this.console_1.Text = "";
            // 
            // app_btn_read
            // 
            this.app_btn_read.Location = new System.Drawing.Point(370, 179);
            this.app_btn_read.Name = "app_btn_read";
            this.app_btn_read.Size = new System.Drawing.Size(75, 30);
            this.app_btn_read.TabIndex = 0;
            this.app_btn_read.Text = "读取";
            this.app_btn_read.UseVisualStyleBackColor = true;
            this.app_btn_read.Click += new System.EventHandler(this.app_btn_read_Click);
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font("宋体", 14F);
            this.label1.Location = new System.Drawing.Point(12, 146);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(196, 30);
            this.label1.TabIndex = 1;
            this.label1.Text = "ModBus地址(Int16):";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBox1
            // 
            this.textBox1.Font = new System.Drawing.Font("宋体", 14F);
            this.textBox1.Location = new System.Drawing.Point(16, 179);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(192, 29);
            this.textBox1.TabIndex = 2;
            this.textBox1.Text = "0";
            // 
            // app_tl_states
            // 
            this.app_tl_states.ColumnCount = 1;
            this.app_tl_states.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.app_tl_states.Controls.Add(this.app_lb_state1, 0, 0);
            this.app_tl_states.Dock = System.Windows.Forms.DockStyle.Right;
            this.app_tl_states.Location = new System.Drawing.Point(643, 3);
            this.app_tl_states.Name = "app_tl_states";
            this.app_tl_states.Padding = new System.Windows.Forms.Padding(3);
            this.app_tl_states.RowCount = 1;
            this.app_tl_states.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.app_tl_states.Size = new System.Drawing.Size(141, 46);
            this.app_tl_states.TabIndex = 1;
            // 
            // app_lb_state1
            // 
            this.app_lb_state1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.app_lb_state1.Font = new System.Drawing.Font("宋体", 12F);
            this.app_lb_state1.Location = new System.Drawing.Point(6, 3);
            this.app_lb_state1.Name = "app_lb_state1";
            this.app_lb_state1.Size = new System.Drawing.Size(129, 40);
            this.app_lb_state1.TabIndex = 0;
            this.app_lb_state1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label2
            // 
            this.label2.Font = new System.Drawing.Font("宋体", 14F);
            this.label2.Location = new System.Drawing.Point(12, 22);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(117, 30);
            this.label2.TabIndex = 1;
            this.label2.Text = "IP 地址:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label3
            // 
            this.label3.Font = new System.Drawing.Font("宋体", 14F);
            this.label3.Location = new System.Drawing.Point(12, 61);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(117, 30);
            this.label3.TabIndex = 1;
            this.label3.Text = "端口:";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBox2
            // 
            this.textBox2.Font = new System.Drawing.Font("宋体", 14F);
            this.textBox2.Location = new System.Drawing.Point(135, 25);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(223, 29);
            this.textBox2.TabIndex = 2;
            this.textBox2.Text = "127.0.0.1";
            // 
            // textBox3
            // 
            this.textBox3.Font = new System.Drawing.Font("宋体", 14F);
            this.textBox3.Location = new System.Drawing.Point(135, 64);
            this.textBox3.Name = "textBox3";
            this.textBox3.Size = new System.Drawing.Size(223, 29);
            this.textBox3.TabIndex = 2;
            this.textBox3.Text = "502";
            // 
            // textBox4
            // 
            this.textBox4.Font = new System.Drawing.Font("宋体", 14F);
            this.textBox4.Location = new System.Drawing.Point(214, 179);
            this.textBox4.Name = "textBox4";
            this.textBox4.Size = new System.Drawing.Size(144, 29);
            this.textBox4.TabIndex = 2;
            this.textBox4.Text = "1";
            // 
            // label4
            // 
            this.label4.Font = new System.Drawing.Font("宋体", 14F);
            this.label4.Location = new System.Drawing.Point(214, 146);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(144, 30);
            this.label4.TabIndex = 1;
            this.label4.Text = "数量";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(789, 589);
            this.Controls.Add(this.app_pn_center);
            this.Controls.Add(this.app_pn_bottom);
            this.Controls.Add(this.app_pn_top);
            this.Name = "Form1";
            this.app_pn_top.ResumeLayout(false);
            this.app_pn_bottom.ResumeLayout(false);
            this.app_pn_center.ResumeLayout(false);
            this.app_pn_center.PerformLayout();
            this.app_tl_states.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button app_btn_connect;
        private System.Windows.Forms.Panel app_pn_top;
        private System.Windows.Forms.Panel app_pn_bottom;
        private System.Windows.Forms.Panel app_pn_center;
        private System.Windows.Forms.RichTextBox console_1;
        private System.Windows.Forms.Button app_btn_read;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TableLayoutPanel app_tl_states;
        private System.Windows.Forms.Label app_lb_state1;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.TextBox textBox3;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBox4;
        private System.Windows.Forms.Label label4;
    }
}

