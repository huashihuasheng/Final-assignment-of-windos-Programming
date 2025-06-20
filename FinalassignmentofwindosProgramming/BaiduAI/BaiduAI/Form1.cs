using AForge.Controls;
using AForge.Video;
using AForge.Video.DirectShow;
using Baidu.Aip.Face;
using BaiduAI.Common;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace BaiduAI
{
    public partial class Form1 : Form
    {
        /*
         * 百度AI平台认证凭据
         * 应用配置参数模块
         * 
         * 开发者需要在百度AI开放平台注册创建应用，获取以下三个关键参数：
         * - APP_ID：应用唯一标识符，用于标识应用身份
         * - API_KEY：API访问密钥，用于接口调用的身份验证
         * - SECRET_KEY：安全密钥，用于生成访问令牌，确保API请求安全性
         * 
         * 注意：在实际生产环境中，这些敏感信息应通过配置文件或环境变量管理，
         * 避免硬编码在源代码中，防止密钥泄露风险。
         */
        private string APP_ID = "6946089";  // 百度AI开放平台的应用ID
        private string API_KEY = "gFrSOXkxkwIuaA556YnAxpqs";  // API访问密钥
        private string SECRET_KEY = "7t8EpdYeuO2g9F1y53DUh6vhtEpMaSGI";  // 安全密钥

        /*
         * 百度AI SDK客户端实例
         * 核心接口调用模块
         * 
         * Face类是百度人脸识别SDK的核心类，负责与百度AI平台通信：
         * - 通过API_KEY和SECRET_KEY完成OAuth2.0认证
         * - 自动管理访问令牌的获取和刷新（SDK内部实现）
         * - 提供人脸检测、比对、搜索、注册等一系列API方法
         */
        private Face client = null;  // 百度人脸识别SDK客户端实例

        // 人脸检测控制相关变量
        /// <summary>
        /// 是否可以进行人脸检测的标志位
        /// 由于百度API调用频率限制，采用间隔检测机制
        /// </summary>
        private bool IsStart = false;

        /// <summary>
        /// 人脸在图像中的位置信息
        /// 用于在视频帧上绘制人脸框
        /// </summary>
        private FaceLocation location = null;

        /* 
         * 视频设备相关属性
         * 使用AForge.NET框架实现摄像头操作
         * 
         * 主要功能：
         * - 枚举系统中所有可用的视频输入设备
         * - 选择并配置指定的摄像头设备
         * - 捕获视频帧并进行处理
         */
        private FilterInfoCollection videoDevices = null;  // 存储系统可用的所有视频输入设备
        private VideoCaptureDevice videoSource;  // 当前使用的视频捕获设备

        // 用于取消后台操作的令牌源
        private CancellationTokenSource _cancellationTokenSource;

        /*
         * 窗体初始化方法
         * 完成UI界面、百度AI SDK客户端初始化等工作 
         */
        public Form1()
        {
            InitializeComponent();

            // 设置Windows Media Player控件为隐藏模式
            // 该控件用于播放系统提示音，如登录成功的提示
            axWindowsMediaPlayer1.uiMode = "Invisible";

            // 初始化百度人脸识别客户端
            // 注意：应在使用前检查API_KEY和SECRET_KEY的有效性
            client = new Face(API_KEY, SECRET_KEY);
        }

        /// <summary>
        /// 图像转Base64编码
        /// </summary>
        /// <param name="file">需要转换的图像对象</param>
        /// <returns>Base64编码字符串</returns>
        public string ConvertImageToBase64(Image file)
        {
            if (file == null)
            {
                return null;
            }

            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    // 明确指定使用JPEG格式保存，避免使用RawFormat可能引发的encoder为null错误
                    file.Save(memoryStream, ImageFormat.Jpeg);
                    byte[] imageBytes = memoryStream.ToArray();  // 获取图像字节数组
                    return Convert.ToBase64String(imageBytes);  // 转换为Base64字符串
                }
            }
            catch (Exception ex)
            {
                ClassLoger.Error("ConvertImageToBase64", ex);
                return null;
            }
        }

        /// <summary>
        /// 选择图片并进行人脸检测按钮点击事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void button1_Click(object sender, EventArgs e)
        {
            // 创建并配置文件选择对话框
            OpenFileDialog dialog = new OpenFileDialog();
            // 使用应用程序当前目录作为初始目录
            dialog.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
            dialog.Filter = "所有文件|*.*";
            dialog.RestoreDirectory = true;
            dialog.FilterIndex = 1;

            // 显示文件选择对话框并处理用户选择
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string filename = dialog.FileName;
                try
                {
                    // 加载选中图片并转换为Base64格式
                    Image im = Image.FromFile(filename);
                    var image = ConvertImageToBase64(im);
                    string imageType = "BASE64";  // 指定图像编码类型为BASE64

                    /* 
                     * 百度人脸检测API参数配置
                     * face_field: 指定需要返回的人脸属性，包括年龄(age)和颜值(beauty)
                     */
                    var options = new Dictionary<string, object>{
                        {"face_field", "age,beauty"},  // 返回人脸的年龄和颜值信息
                        {"face_fields", "age,qualities,beauty"}  // 返回人脸的年龄、质量和颜值信息
                    };

                    // 调用百度AI人脸检测API
                    var result = client.Detect(image, imageType, options);

                    // 将检测结果显示在文本框中
                    textBox1.Text = result.ToString();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        /// <summary>
        /// 从文件中读取图片并转换为Base64字符串
        /// </summary>
        /// <param name="img">图片文件路径</param>
        /// <returns>Base64编码的字符串</returns>
        public string ReadImg(string img)
        {
            return Convert.ToBase64String(File.ReadAllBytes(img));  // 直接读取文件字节并转为Base64
        }

        /// <summary>
        /// 人脸比对按钮点击事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void button2_Click(object sender, EventArgs e)
        {
            // 检查是否已选择两张待比对的图片
            if (string.IsNullOrEmpty(textBox2.Text) || string.IsNullOrEmpty(textBox3.Text))
            {
                MessageBox.Show("请选择要对比的人脸图片");
                return;
            }
            try
            {
                string path1 = textBox2.Text;  // 第一张人脸图片路径
                string path2 = textBox3.Text;  // 第二张人脸图片路径

                /* 
                 * 构建人脸比对所需的JSON数组
                 * 使用Newtonsoft.Json.Linq创建JSON结构
                 * 每个JObject包含一张人脸图片的相关参数
                 */
                var faces = new JArray
                {
                    new JObject
                    {
                        {"image", ReadImg(path1)},  // 第一张图片的Base64编码
                        {"image_type", "BASE64"},   // 图片编码类型
                        {"face_type", "LIVE"},      // 人脸类型：生活照
                        {"quality_control", "LOW"}, // 质量控制：低级别
                        {"liveness_control", "NONE"}, // 活体检测：不做活体检测
                    },
                    new JObject
                    {
                        {"image", ReadImg(path2)},  // 第二张图片的Base64编码
                        {"image_type", "BASE64"},   // 图片编码类型
                        {"face_type", "LIVE"},      // 人脸类型：生活照
                        {"quality_control", "LOW"}, // 质量控制：低级别
                        {"liveness_control", "NONE"}, // 活体检测：不做活体检测
                    }
                 };

                // 调用百度AI人脸比对API，传入包含两张人脸图片信息的JArray
                var result = client.Match(faces);
                // 显示比对结果，包含相似度分数和其他信息
                textBox1.Text = result.ToString();
            }
            catch (Exception ex)
            {
                // 异常处理，此处未显示错误信息
            }
        }

        /// <summary>
        /// 选择人脸比对图片按钮点击事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void button3_Click(object sender, EventArgs e)
        {
            // 创建并配置文件选择对话框
            OpenFileDialog dialog = new OpenFileDialog();
            // 使用应用程序当前目录作为初始目录
            dialog.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
            dialog.Filter = "所有文件|*.*";
            dialog.RestoreDirectory = true;
            dialog.FilterIndex = 2;

            // 显示文件选择对话框并处理用户选择
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                // 根据是否已有第一张图片，决定填充哪个文本框
                if (string.IsNullOrEmpty(textBox2.Text))
                {
                    textBox2.Text = dialog.FileName;  // 填充第一张图片路径
                }
                else
                {
                    textBox3.Text = dialog.FileName;  // 填充第二张图片路径
                }
            }
        }

        /// <summary>
        /// 窗体加载事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void Form1_Load(object sender, EventArgs e)
        {
            // 初始化取消令牌源，用于取消后台操作
            _cancellationTokenSource = new CancellationTokenSource();

            /* 
             * 视频设备初始化
             * 枚举系统中的所有视频输入设备并加载到下拉列表中
             */
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (videoDevices != null && videoDevices.Count > 0)
            {
                foreach (FilterInfo device in videoDevices)
                {
                    comboBox1.Items.Add(device.Name);  // 将设备名称添加到下拉列表
                }
                comboBox1.SelectedIndex = 0;  // 默认选择第一个设备
            }

            // 注册视频帧捕获事件处理器
            // 当视频源有新帧时，会触发VideoSourcePlayer1_NewFrame方法
            videoSourcePlayer1.NewFrame += VideoSourcePlayer1_NewFrame;

            /* 
             * 启动人脸检测定时器线程
             * 由于百度AI平台人脸识别接口限制每秒最多调用2次
             * 使用线程池实现定时启动人脸检测
             */
            /*
            ThreadPool.QueueUserWorkItem(state => {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    IsStart = true;
                    try
                    {
                        // 使用可取消的等待
                        Task.Delay(500, _cancellationTokenSource.Token).Wait();
                    }
                    catch (OperationCanceledException)
                    {
                        break; // 优雅退出
                    }
                }
            });*/
        }

        /// <summary>
        /// 视频帧捕获事件处理方法
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="image">捕获的图像帧</param>
        private void VideoSourcePlayer1_NewFrame(object sender, ref Bitmap image)
        {
            try
            {
                if (IsStart)
                {
                    IsStart = false;  // 重置检测标志
                }

                // 如果检测到人脸位置信息，在视频帧上绘制人脸框
                if (location != null)
                {
                    try
                    {
                        // 使用Graphics绘制四条线构成人脸框
                        Graphics g = Graphics.FromImage(image);
                        // 上边框线
                        g.DrawLine(new Pen(Color.Black), new System.Drawing.Point(location.left, location.top), new System.Drawing.Point(location.left + location.width, location.top));
                        // 左边框线
                        g.DrawLine(new Pen(Color.Black), new System.Drawing.Point(location.left, location.top), new System.Drawing.Point(location.left, location.top + location.height));
                        // 下边框线
                        g.DrawLine(new Pen(Color.Black), new System.Drawing.Point(location.left, location.top + location.height), new System.Drawing.Point(location.left + location.width, location.top + location.height));
                        // 右边框线
                        g.DrawLine(new Pen(Color.Black), new System.Drawing.Point(location.left + location.width, location.top), new System.Drawing.Point(location.left + location.width, location.top + location.height));
                        g.Dispose();  // 释放Graphics资源，避免内存泄漏
                    }
                    catch (Exception ex)
                    {
                        ClassLoger.Error("VideoSourcePlayer1_NewFrame", ex);  // 记录绘制异常
                    }
                }
            }
            catch (Exception ex)
            {
                ClassLoger.Error("VideoSourcePlayer1_NewFrame1", ex);  // 记录事件处理异常
            }
        }

        /// <summary>
        /// 连接并打开摄像头
        /// </summary>
        private void CameraConn()
        {
            // 检查是否有可用的视频设备
            if (comboBox1.Items.Count <= 0)
            {
                MessageBox.Show("请插入视频设备");
                return;
            }

            // 创建视频捕获设备对象
            videoSource = new VideoCaptureDevice(videoDevices[comboBox1.SelectedIndex].MonikerString);
            // 设置视频分辨率为320x240
            videoSource.DesiredFrameSize = new System.Drawing.Size(320, 240);
            // 设置帧率为1帧/秒
            videoSource.DesiredFrameRate = 1;

            // 将视频源设置到视频播放控件
            videoSourcePlayer1.VideoSource = videoSource;
            // 启动视频流
            videoSourcePlayer1.Start();
        }

        /// <summary>
        /// 重新检测视频设备按钮点击事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void button6_Click(object sender, EventArgs e)
        {
            // 重新获取系统中的视频设备
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (videoDevices != null && videoDevices.Count > 0)
            {
                foreach (FilterInfo device in videoDevices)
                {
                    comboBox1.Items.Add(device.Name);  // 添加到下拉列表
                }
                comboBox1.SelectedIndex = 0;  // 选择第一个设备
            }
        }

        /// <summary>
        /// 拍照按钮点击事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void button5_Click(object sender, EventArgs e)
        {
            // 检查是否有可用的视频设备
            if (comboBox1.Items.Count <= 0)
            {
                MessageBox.Show("请插入视频设备");
                return;
            }
            try
            {
                // 检查视频是否在运行
                if (videoSourcePlayer1.IsRunning)
                {
                    // 获取当前视频帧
                    Bitmap currentFrame = videoSourcePlayer1.GetCurrentVideoFrame();
                    if (currentFrame == null)
                    {
                        MessageBox.Show("无法获取视频帧", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // 创建一个较小尺寸的Bitmap来减少内存消耗
                    Bitmap resizedFrame = null;
                    string picName = GetImagePath() + "\\" + DateTime.Now.ToFileTime() + ".jpg";

                    try
                    {
                        // 将大图像缩小到更合理的尺寸以减少内存消耗
                        int maxWidth = 800;
                        int maxHeight = 600;

                        // 计算新尺寸，保持宽高比
                        double ratioX = (double)maxWidth / currentFrame.Width;
                        double ratioY = (double)maxHeight / currentFrame.Height;
                        double ratio = Math.Min(ratioX, ratioY);

                        int newWidth = (int)(currentFrame.Width * ratio);
                        int newHeight = (int)(currentFrame.Height * ratio);

                        // 创建缩小的图像
                        resizedFrame = new Bitmap(newWidth, newHeight);
                        using (Graphics g = Graphics.FromImage(resizedFrame))
                        {
                            g.DrawImage(currentFrame, 0, 0, newWidth, newHeight);
                        }

                        // 直接保存JPEG图像到文件系统
                        if (File.Exists(picName))
                        {
                            File.Delete(picName);
                        }

                        // 保存为JPEG格式
                        resizedFrame.Save(picName, ImageFormat.Jpeg);

                        try
                        {
                            // 获取当前视频帧作为图片对象
                            Image imageForDetect = resizedFrame;

                            // 转换为Base64格式
                            var imageBase64 = ConvertImageToBase64(imageForDetect);

                            string imageType = "BASE64";

                            // 记录所有API参数
                            var options = new Dictionary<string, object>{
                                {"face_field", "age,beauty"}
                            };

                            // 调用百度AI人脸检测API
                            var result = client.Detect(imageBase64, imageType, options);

                            // 检查是否成功检测到人脸
                            if (result["error_code"].Value<int>() == 0 &&
                                result["result"] != null &&
                                result["result"]["face_list"] != null &&
                                result["result"]["face_list"].Count() > 0)
                            {
                                // 获取人脸信息
                                JToken faceInfo = result["result"]["face_list"][0];
                                string age = faceInfo["age"].ToString();
                                string beauty = faceInfo["beauty"].ToString();

                                // 保存人脸位置信息用于显示
                                this.location = new FaceLocation
                                {
                                    left = (int)faceInfo["location"]["left"],
                                    top = (int)faceInfo["location"]["top"],
                                    width = (int)faceInfo["location"]["width"],
                                    height = (int)faceInfo["location"]["height"]
                                };

                                ageText.Text = age;
                                textBox4.Text = beauty;

                                // 显示合并的保存和人脸信息消息
                                MessageBox.Show($"图片已成功保存至：\n{picName}\n\n人脸信息：\n年龄：{age}\n美颜度：{beauty}",
                                    "拍照成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                // 未检测到人脸的情况
                                MessageBox.Show($"图片已成功保存至：\n{picName}\n\n未检测到人脸信息",
                                    "拍照成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                        catch (Exception ex)
                        {
                            // 人脸分析发生错误但图片已保存
                            MessageBox.Show($"图片已成功保存至：\n{picName}\n\n人脸分析异常：{ex.Message}",
                                "拍照成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            // 记录错误到日志
                            ClassLoger.Error("button5_Click", ex);
                        }
                    }
                    finally
                    {
                        // 确保释放非托管资源
                        if (resizedFrame != null)
                        {
                            resizedFrame.Dispose();
                        }
                        if (currentFrame != null)
                        {
                            currentFrame.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("拍照过程中发生错误：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 获取图片保存路径
        /// </summary>
        /// <returns>图片保存目录路径</returns>
        private string GetImagePath()
        {
            // 构建PersonImg目录路径
            string personImgPath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory)
                         + Path.DirectorySeparatorChar.ToString() + "PersonImg";

            // 如果目录不存在，创建它
            if (!Directory.Exists(personImgPath))
            {
                Directory.CreateDirectory(personImgPath);
            }

            return personImgPath;
        }

        /// <summary>
        /// 启动摄像头按钮点击事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void button4_Click(object sender, EventArgs e)
        {
            CameraConn();  // 调用连接摄像头方法
        }

        /// <summary>
        /// Bitmap转byte[]数组
        /// </summary>
        /// <param name="bitmap">位图对象</param>
        /// <returns>字节数组</returns>
        public byte[] Bitmap2Byte(Bitmap bitmap)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    // 将位图保存为JPEG格式到内存流
                    bitmap.Save(stream, ImageFormat.Jpeg);
                    byte[] data = new byte[stream.Length];
                    // 将流指针移到开始位置
                    stream.Seek(0, SeekOrigin.Begin);
                    // 读取全部字节到数组
                    stream.Read(data, 0, Convert.ToInt32(stream.Length));
                    return data;
                }
            }
            catch (Exception ex) { }
            return null;
        }

        /// <summary>
        /// BitmapSource转byte[]数组
        /// </summary>
        /// <param name="source">BitmapSource对象</param>
        /// <returns>字节数组</returns>
        public byte[] BitmapSource2Byte(BitmapSource source)
        {
            try
            {
                // 创建JPEG编码器，设置最高质量
                JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.QualityLevel = 100;

                using (MemoryStream stream = new MemoryStream())
                {
                    // 添加图像帧并保存到内存流
                    encoder.Frames.Add(BitmapFrame.Create(source));
                    encoder.Save(stream);
                    // 获取字节数组
                    byte[] bit = stream.ToArray();
                    stream.Close();
                    return bit;
                }
            }
            catch (Exception ex)
            {
                ClassLoger.Error("BitmapSource2Byte", ex);
            }
            return null;
        }

        /// <summary>
        /// 人脸检测核心方法
        /// </summary>
        /// <param name="image">待检测的图像对象</param>
        public void Detect(object image)
        {
            // 检查图像对象有效性
            if (image != null && image is Bitmap)
            {
                Bitmap img = null;
                try
                {
                    // 图像预处理：转换为Bitmap并获取字节数据
                    img = (Bitmap)image;

                    // 创建较小的副本以减少内存占用
                    Bitmap smallerImg = null;
                    try
                    {
                        // 将大图像缩小到更合理的尺寸以减少内存消耗
                        int maxWidth = 400; // 用于实时检测的更小尺寸
                        int maxHeight = 300;

                        // 计算新尺寸，保持宽高比
                        double ratioX = (double)maxWidth / img.Width;
                        double ratioY = (double)maxHeight / img.Height;
                        double ratio = Math.Min(ratioX, ratioY);

                        if (ratio < 1.0) // 只有在图像大于目标尺寸时才调整大小
                        {
                            int newWidth = (int)(img.Width * ratio);
                            int newHeight = (int)(img.Height * ratio);

                            // 创建缩小的图像
                            smallerImg = new Bitmap(newWidth, newHeight);
                            using (Graphics g = Graphics.FromImage(smallerImg))
                            {
                                g.DrawImage(img, 0, 0, newWidth, newHeight);
                            }
                        }
                        else
                        {
                            // 如果图像已经足够小，直接克隆
                            smallerImg = new Bitmap(img);
                        }

                        var imgByte = Bitmap2Byte(smallerImg);

                        // 转换为Base64格式
                        string image1 = ConvertImageToBase64(smallerImg);
                        string imageType = "BASE64";

                        if (imgByte != null)
                        {
                            /* 
                             * 人脸检测API参数配置
                             * max_face_num: 最多检测的人脸数量，设为2
                             * face_field: 需要返回的人脸属性，包括年龄、质量和颜值
                             */
                            var options = new Dictionary<string, object>{
                                {"max_face_num", 2},  // 检测最多2个人脸
                                {"face_field", "age,qualities,beauty"}  // 返回年龄、质量和颜值信息
                            };

                            // 调用百度AI人脸检测API
                            var result = client.Detect(image1, imageType, options);

                            // 将JSON结果反序列化为FaceDetectInfo对象
                            FaceDetectInfo detect = JsonHelper.DeserializeObject<FaceDetectInfo>(result.ToString());

                            // 处理检测结果
                            if (detect != null && detect.result_num > 0)
                            {
                                // 需要使用Invoke在UI线程上更新UI控件
                                this.Invoke((MethodInvoker)delegate {
                                    // 显示检测到的人脸年龄
                                    ageText.Text = detect.result[0].age.TryToString();
                                    // 保存人脸位置信息，用于绘制人脸框
                                    this.location = detect.result[0].location;

                                    // 构建人脸质量提示信息
                                    StringBuilder sb = new StringBuilder();

                                    /* 
                                     * 人脸质量分析
                                     * 基于百度AI返回的人脸质量参数进行分析
                                     * 针对不同质量问题给出具体提示
                                     */
                                    if (detect.result[0].qualities != null)
                                    {
                                        // 检查人脸模糊度
                                        if (detect.result[0].qualities.blur >= 0.7)
                                        {
                                            sb.AppendLine("人脸过于模糊");
                                        }
                                        // 检查人脸完整度
                                        if (detect.result[0].qualities.completeness >= 0.4)
                                        {
                                            sb.AppendLine("人脸不完整");
                                        }
                                        // 检查光照条件
                                        if (detect.result[0].qualities.illumination <= 40)
                                        {
                                            sb.AppendLine("灯光光线质量不好");
                                        }

                                        // 检查面部遮挡情况
                                        if (detect.result[0].qualities.occlusion != null)
                                        {
                                            // 左脸颊遮挡检测
                                            if (detect.result[0].qualities.occlusion.left_cheek >= 0.8)
                                            {
                                                sb.AppendLine("左脸颊不清晰");
                                            }
                                            // 左眼遮挡检测
                                            if (detect.result[0].qualities.occlusion.left_eye >= 0.6)
                                            {
                                                sb.AppendLine("左眼不清晰");
                                            }
                                            // 嘴巴遮挡检测
                                            if (detect.result[0].qualities.occlusion.mouth >= 0.7)
                                            {
                                                sb.AppendLine("嘴巴不清晰");
                                            }
                                            // 鼻子遮挡检测
                                            if (detect.result[0].qualities.occlusion.nose >= 0.7)
                                            {
                                                sb.AppendLine("鼻子不清晰");
                                            }
                                            // 右脸颊遮挡检测
                                            if (detect.result[0].qualities.occlusion.right_cheek >= 0.8)
                                            {
                                                sb.AppendLine("右脸颊不清晰");
                                            }
                                            // 右眼遮挡检测
                                            if (detect.result[0].qualities.occlusion.right_eye >= 0.6)
                                            {
                                                sb.AppendLine("右眼不清晰");
                                            }
                                            // 下巴遮挡检测
                                            if (detect.result[0].qualities.occlusion.chin >= 0.6)
                                            {
                                                sb.AppendLine("下巴不清晰");
                                            }

                                            /* 
                                             * 人脸姿态分析
                                             * 分析人脸的俯仰角(pitch)、横滚角(roll)和偏航角(yaw)
                                             * 给出调整建议
                                             */
                                            if (detect.result[0].pitch >= 20)
                                            {
                                                sb.AppendLine("俯视角度太大");
                                            }
                                            if (detect.result[0].roll >= 20)
                                            {
                                                sb.AppendLine("脸部应该放正");
                                            }
                                            if (detect.result[0].yaw >= 20)
                                            {
                                                sb.AppendLine("脸部应该放正点");
                                            }
                                        }
                                    }

                                    // 检查人脸尺寸是否过小
                                    if (detect.result[0].location.height <= 100 || detect.result[0].location.width <= 100)
                                    {
                                        sb.AppendLine("人脸部分过小");
                                    }

                                    // 显示质量分析结果
                                    textBox4.Text = sb.ToString();
                                    // 如果没有质量问题，显示"OK"
                                    if (textBox4.Text.IsNull())
                                    {
                                        textBox4.Text = "OK";
                                    }
                                });
                            }
                        }
                    }
                    finally
                    {
                        // 释放资源
                        if (smallerImg != null)
                        {
                            smallerImg.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    ClassLoger.Error("Form1.Detect", ex);
                }
                finally
                {
                    // 释放原始图像资源
                    if (img != null && img != image) // 只有在我们创建了新的Bitmap时才释放
                    {
                        img.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// 窗体关闭事件处理
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            // 请求取消所有后台操作
            _cancellationTokenSource?.Cancel();

            // 停止所有线程、释放资源
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.Stop();
            }

            // 等待一段时间确保线程有机会清理
            Thread.Sleep(100);

            // 退出应用程序
            System.Environment.Exit(0);
        }

        /// <summary>
        /// 人脸注册按钮点击事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void button7_Click(object sender, EventArgs e)
        {
            string userInfo = textBox6.Text.Trim();  // 用户资料（即用户名）
            string groupId = textBox5.Text.Trim();  // 用户组ID

            // 检查用户名是否为空
            if (string.IsNullOrEmpty(userInfo))
            {
                MessageBox.Show("请输入用户名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 检查是否有可用的视频设备
            if (comboBox1.Items.Count <= 0)
            {
                MessageBox.Show("请插入视频设备");
                return;
            }

            try
            {
                // 检查视频是否在运行
                if (videoSourcePlayer1.IsRunning)
                {
                    // 获取当前视频帧
                    Bitmap currentFrame = videoSourcePlayer1.GetCurrentVideoFrame();

                    // 转换为字节数组
                    byte[] imageBytes = Bitmap2Byte(currentFrame);

                    // 检查转换结果
                    if (imageBytes == null)
                    {
                        MessageBox.Show("无法捕获图像，请检查摄像头连接", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // 转换为Base64
                    string imageBase64 = Convert.ToBase64String(imageBytes);

                    // 人脸注册API参数配置 - 使用固定用户ID "1"，但保存用户资料
                    var options = new Dictionary<string, object>{
                        {"action_type", "REPLACE"},  // 替换模式
                        {"user_info", userInfo}  // 保存用户名作为用户资料
                    };

                    // 调用百度AI人脸注册API
                    var result = client.UserAdd(imageBase64, "BASE64", groupId, "1", options);

                    // 处理注册结果
                    if (result.Value<int>("error_code") == 0)
                    {
                        MessageBox.Show($"注册成功！用户名: {userInfo}");
                    }
                    else
                    {
                        MessageBox.Show("注册失败:" + result.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("摄像头异常：" + ex.Message);
            }
        }

        /// <summary>
        /// 人脸登录按钮点击事件
        /// </summary>
        private void button8_Click(object sender, EventArgs e)
        {
            string groupId = textBox5.Text.Trim();  // 使用与注册相同的用户组ID

            // 检查组ID不能为空
            if (string.IsNullOrEmpty(groupId))
            {
                MessageBox.Show("请输入用户组ID", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 检查是否有可用的视频设备
            if (comboBox1.Items.Count <= 0)
            {
                MessageBox.Show("请插入视频设备");
                return;
            }

            try
            {
                // 检查视频是否在运行
                if (videoSourcePlayer1.IsRunning)
                {
                    // 获取当前视频帧
                    Bitmap currentFrame = videoSourcePlayer1.GetCurrentVideoFrame();

                    // 转换为字节数组
                    byte[] imageBytes = Bitmap2Byte(currentFrame);

                    // 检查转换结果
                    if (imageBytes == null)
                    {
                        MessageBox.Show("无法捕获图像，请检查摄像头连接", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // 转换为Base64
                    string imageBase64 = Convert.ToBase64String(imageBytes);

                    // 人脸搜索API参数配置
                    var options = new Dictionary<string, object>{
                        {"match_threshold", 70},  // 匹配阈值为70
                        {"quality_control", "NORMAL"},  // 中等质量控制
                        {"liveness_control", "LOW"},  // 低级别活体检测
                        {"max_user_num", 3}  // 最多返回3个匹配用户
                    };

                    // 调用百度AI人脸搜索API
                    var result = client.Search(imageBase64, "BASE64", groupId, options);

                    // 输出完整的API返回结果用于调试
                    Console.WriteLine("百度AI人脸搜索API返回结果: " + result.ToString());

                    // 处理搜索结果
                    if (result.Value<int>("error_code") == 0)
                    {
                        // 检查是否有匹配的用户
                        if (result["result"] != null &&
                            result["result"]["user_list"] != null &&
                            result["result"]["user_list"].Count() > 0)
                        {
                            // 获取匹配的用户列表
                            JArray userList = result["result"].Value<JArray>("user_list");
                            var matchedUser = userList[0];

                            // 安全地获取用户信息
                            string userName = "";
                            double score = 0;

                            try
                            {
                                // 尝试获取相似度分数
                                score = matchedUser.Value<double>("score");

                                // 尝试从不同可能的字段获取用户名
                                if (matchedUser["user_info"] != null)
                                {
                                    userName = matchedUser.Value<string>("user_info");
                                }
                                else if (matchedUser["user_name"] != null)
                                {
                                    userName = matchedUser.Value<string>("user_name");
                                }
                                else
                                {
                                    // 如果找不到用户信息字段，使用默认值
                                    userName = "未知用户";
                                    MessageBox.Show("未找到用户信息字段，但人脸匹配成功。请确保注册时正确保存了用户信息。",
                                        "信息不完整", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                            }
                            catch (Exception ex)
                            {
                                // 记录解析异常
                                ClassLoger.Error("解析百度AI返回的用户信息失败", ex);
                                userName = "解析失败";
                            }

                            // 显示用户名
                            textBox7.Text = userName;

                            // 播放登录成功提示音
                            axWindowsMediaPlayer1.URL = "20230522_160638_1.mp3";
                            axWindowsMediaPlayer1.Ctlcontrols.play();

                            MessageBox.Show($"登录成功！用户名: {userName}, 相似度: {score:F2}",
                                "登录成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("未找到匹配的用户，请先注册或检查用户组ID是否正确",
                                "未找到用户", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    else
                    {
                        MessageBox.Show($"登录失败: {result["error_msg"]}",
                            "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("摄像头异常：" + ex.Message);
            }
        }

        /// <summary>
        /// 停止按钮点击事件
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void button9_Click(object sender, EventArgs e)
        {
            // 停止音频播放
            axWindowsMediaPlayer1.Ctlcontrols.stop();

            // 检查视频设备是否可用
            if (videoDevices == null || videoDevices.Count == 0)
            {
                return;
            }

            // 停止视频采集
            videoSource.Stop();
            videoSourcePlayer1.Stop();
        }

        private void ageText_TextChanged(object sender, EventArgs e)
        {
            // 此方法为空，可能需要实现年龄文本变化时的处理逻辑
        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {
            // 此方法为空，可能需要实现文本框内容变化时的处理逻辑
        }
    }
}