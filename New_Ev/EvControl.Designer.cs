namespace New_Ev
{
    partial class EvControl
    {
        /// <summary> 
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 구성 요소 디자이너에서 생성한 코드

        /// <summary> 
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            btnStartEv = new Button();
            lblEvState = new Label();
            SuspendLayout();
            // 
            // btnStartEv
            // 
            btnStartEv.Location = new Point(199, 84);
            btnStartEv.Name = "btnStartEv";
            btnStartEv.Size = new Size(118, 29);
            btnStartEv.TabIndex = 0;
            btnStartEv.Text = "Ev 연결 시작";
            btnStartEv.UseVisualStyleBackColor = true;
            btnStartEv.Click += btnStartEv_Click;
            // 
            // lblEvState
            // 
            lblEvState.AutoSize = true;
            lblEvState.Location = new Point(237, 344);
            lblEvState.Name = "lblEvState";
            lblEvState.Size = new Size(54, 20);
            lblEvState.TabIndex = 1;
            lblEvState.Text = "대기중";
            // 
            // EvControl
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(lblEvState);
            Controls.Add(btnStartEv);
            Name = "EvControl";
            Size = new Size(559, 442);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnStartEv;
        private Label lblEvState;
    }
}
