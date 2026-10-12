using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace GmuLauncher.Views;

/// <summary>
/// معاينة ثلاثية الأبعاد للـ Skin باستخدام WPF 3D الأصلي (بدون مكتبات إضافية).
/// نبني كل جزء من الجسم كمجسم (Box) ونطبّق إحداثيات الـ UV حسب تخطيط ملف skin القياسي 64x64.
/// </summary>
public partial class SkinViewer : UserControl
{
    public static readonly DependencyProperty SkinPathProperty = DependencyProperty.Register(
        nameof(SkinPath), typeof(string), typeof(SkinViewer),
        new PropertyMetadata(null, (d, _) => ((SkinViewer)d).Rebuild()));

    public string? SkinPath { get => (string?)GetValue(SkinPathProperty); set => SetValue(SkinPathProperty, value); }

    readonly AxisAngleRotation3D _rotY = new(new Vector3D(0, 1, 0), 25);
    readonly AxisAngleRotation3D _rotX = new(new Vector3D(1, 0, 0), 0);
    ModelVisual3D? _model;
    double _tw = 64, _th = 64;
    Point _last; bool _drag;

    public SkinViewer()
    {
        InitializeComponent();
        // تدوير الشخصية بسحب الماوس
        Host.MouseLeftButtonDown += (_, e) => { _drag = true; _last = e.GetPosition(Host); Host.CaptureMouse(); };
        Host.MouseLeftButtonUp += (_, _) => { _drag = false; Host.ReleaseMouseCapture(); };
        Host.MouseMove += (_, e) =>
        {
            if (!_drag) return;
            var p = e.GetPosition(Host);
            _rotY.Angle += (p.X - _last.X) * 0.8;
            _rotX.Angle = Math.Clamp(_rotX.Angle + (p.Y - _last.Y) * 0.4, -40, 40);
            _last = p;
        };
    }

    void Rebuild()
    {
        if (_model != null) { View.Children.Remove(_model); _model = null; }
        if (string.IsNullOrEmpty(SkinPath) || !System.IO.File.Exists(SkinPath)) return;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(SkinPath);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            _tw = bmp.PixelWidth; _th = bmp.PixelHeight;

            var brush = new ImageBrush(bmp);
            RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.NearestNeighbor);   // بكسلات حادة
            var mat = new DiffuseMaterial(brush);
            bool modern = _th >= 64;   // الصيغة القديمة 64x32 تستخدم نفس ملمس الذراع/الرجل اليمنى لليسرى

            var g = new Model3DGroup();
            //            x   y   z   w  h  d    u   v
            AddBox(g, mat, -4, 24, -4, 8, 8, 8, 0, 0);                                  // الرأس
            AddBox(g, mat, -4, 12, -2, 8, 12, 4, 16, 16);                               // الجسم
            AddBox(g, mat, -8, 12, -2, 4, 12, 4, 40, 16);                               // الذراع اليمنى
            AddBox(g, mat, 4, 12, -2, 4, 12, 4, modern ? 32 : 40, modern ? 48 : 16);    // الذراع اليسرى
            AddBox(g, mat, -4, 0, -2, 4, 12, 4, 0, 16);                                 // الرجل اليمنى
            AddBox(g, mat, 0, 0, -2, 4, 12, 4, modern ? 16 : 0, modern ? 48 : 16);      // الرجل اليسرى

            var tg = new Transform3DGroup();
            tg.Children.Add(new RotateTransform3D(_rotY, new Point3D(0, 16, 0)));
            tg.Children.Add(new RotateTransform3D(_rotX, new Point3D(0, 16, 0)));
            g.Transform = tg;

            _model = new ModelVisual3D { Content = g };
            View.Children.Add(_model);
        }
        catch { }
    }

    void AddBox(Model3DGroup g, Material m, double x, double y, double z, double w, double h, double d, double u, double v)
    {
        var mesh = new MeshGeometry3D();
        double x0 = x, x1 = x + w, y0 = y, y1 = y + h, z0 = z, z1 = z + d;
        // كل وجه: 4 نقاط (يسار-أسفل، يمين-أسفل، يمين-أعلى، يسار-أعلى) كما تُرى من الخارج + مستطيل الملمس
        Face(mesh, new(x0, y0, z1), new(x1, y0, z1), new(x1, y1, z1), new(x0, y1, z1), u + d, v + d, w, h);               // أمام
        Face(mesh, new(x1, y0, z0), new(x0, y0, z0), new(x0, y1, z0), new(x1, y1, z0), u + 2 * d + w, v + d, w, h);       // خلف
        Face(mesh, new(x0, y0, z0), new(x0, y0, z1), new(x0, y1, z1), new(x0, y1, z0), u, v + d, d, h);                   // يمين
        Face(mesh, new(x1, y0, z1), new(x1, y0, z0), new(x1, y1, z0), new(x1, y1, z1), u + d + w, v + d, d, h);           // يسار
        Face(mesh, new(x0, y1, z1), new(x1, y1, z1), new(x1, y1, z0), new(x0, y1, z0), u + d, v, w, d);                   // أعلى
        Face(mesh, new(x0, y0, z0), new(x1, y0, z0), new(x1, y0, z1), new(x0, y0, z1), u + d + w, v, w, d);               // أسفل
        mesh.Freeze();
        g.Children.Add(new GeometryModel3D(mesh, m) { BackMaterial = m });
    }

    void Face(MeshGeometry3D mesh, Point3D a, Point3D b, Point3D c, Point3D d, double u, double v, double w, double h)
    {
        int i = mesh.Positions.Count;
        mesh.Positions.Add(a); mesh.Positions.Add(b); mesh.Positions.Add(c); mesh.Positions.Add(d);
        double u0 = u / _tw, u1 = (u + w) / _tw, v0 = v / _th, v1 = (v + h) / _th;
        mesh.TextureCoordinates.Add(new Point(u0, v1));
        mesh.TextureCoordinates.Add(new Point(u1, v1));
        mesh.TextureCoordinates.Add(new Point(u1, v0));
        mesh.TextureCoordinates.Add(new Point(u0, v0));
        foreach (var k in new[] { 0, 1, 2, 0, 2, 3 }) mesh.TriangleIndices.Add(i + k);
    }
}
