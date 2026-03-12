using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ThermalDoctor.Helpers;
using ThermalDoctor.Models;
using ThermalDoctor.ViewModels;

namespace ThermalDoctor.Views;

public partial class DeviceVisualization3D : UserControl
{
    // Trackball state
    private bool _isRotating;
    private bool _isPanning;
    private Point _lastMousePos;
    private double _rotationX = 25;   // vertical angle
    private double _rotationY = -35;  // horizontal angle
    private double _distance = 14.0;
    private Point3D _lookAt = new(0, 0.5, 0);

    // Scene management
    private readonly Model3DGroup _sceneGroup = new();
    private readonly Dictionary<string, GeometryModel3D> _componentModels = new();
    private readonly Dictionary<string, Border> _componentLabels = new();

    public DeviceVisualization3D()
    {
        InitializeComponent();
        SceneRoot.Content = _sceneGroup;
        UpdateCamera();
        DataContextChanged += OnDataContextChanged;
        CompositionTarget.Rendering += OnRender;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
        {
            oldVm.Readings.CollectionChanged -= OnReadingsChanged;
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is MainViewModel vm)
        {
            vm.Readings.CollectionChanged += OnReadingsChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            RebuildScene();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "DeviceModelName" or "DeviceOutlinePath" or "CurrentFormFactor")
        {
            RebuildScene();
        }
        else if (e.PropertyName is "IsDarkTheme")
        {
            RebuildScene();
        }
        else if (e.PropertyName is "UseFahrenheit")
        {
            UpdateLabels();
        }
    }

    private void OnReadingsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateComponentColors();
        UpdateLabels();
    }

    // ───────────────────────────────────────────────────────────
    //  Scene Building
    // ───────────────────────────────────────────────────────────

    private void RebuildScene()
    {
        _sceneGroup.Children.Clear();
        _componentModels.Clear();
        _componentLabels.Clear();
        LabelOverlay.Children.Clear();

        if (DataContext is not MainViewModel vm)
            return;

        var formFactor = vm.CurrentFormFactor;

        if (formFactor == DeviceFormFactor.Laptop)
            BuildLaptopShell();
        else if (formFactor == DeviceFormFactor.Studio)
            BuildStudioShell();
        else
            BuildTabletShell();

        // Add components
        foreach (var reading in vm.Readings)
        {
            AddComponent3D(reading, formFactor == DeviceFormFactor.Laptop);
        }

        // Ground grid for depth reference
        AddGroundGrid();
    }

    private void BuildLaptopShell()
    {
        // Laptop base (keyboard deck) — flat box
        var baseMat = CreateGlassMaterial(Color.FromArgb(40, 120, 130, 180));
        AddBox(_sceneGroup, new Point3D(0, 0, 0.5), 5.0, 0.15, 3.2, baseMat);

        // Lid — angled panel rising from back
        var lidMat = CreateGlassMaterial(Color.FromArgb(35, 100, 120, 200));
        AddAngledLid(_sceneGroup, lidMat);

        // Screen on lid (dark panel)
        var screenMat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(80, 20, 20, 50)));
        AddAngledScreen(_sceneGroup, screenMat);

        // Keyboard area hint
        var kbMat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(30, 50, 50, 80)));
        AddBox(_sceneGroup, new Point3D(0, 0.08, 0.3), 3.6, 0.01, 1.8, kbMat);

        // Trackpad
        var tpMat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(25, 70, 70, 100)));
        AddBox(_sceneGroup, new Point3D(0, 0.08, 1.7), 1.4, 0.01, 0.7, tpMat);

        // Edge accents
        var edgeMat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(60, 160, 170, 200)));
        // Front edge
        AddBox(_sceneGroup, new Point3D(0, 0, 2.1), 5.0, 0.15, 0.04, edgeMat);
        // Back edge
        AddBox(_sceneGroup, new Point3D(0, 0, -1.1), 5.0, 0.15, 0.04, edgeMat);
        // Left edge
        AddBox(_sceneGroup, new Point3D(-2.5, 0, 0.5), 0.04, 0.15, 3.2, edgeMat);
        // Right edge
        AddBox(_sceneGroup, new Point3D(2.5, 0, 0.5), 0.04, 0.15, 3.2, edgeMat);

        // Hinge
        var hingeMat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(80, 180, 180, 200)));
        AddBox(_sceneGroup, new Point3D(0, 0.14, -1.0), 4.8, 0.06, 0.12, hingeMat);
    }

    private void BuildTabletShell()
    {
        // Tablet body
        var bodyMat = CreateGlassMaterial(Color.FromArgb(40, 120, 130, 180));
        AddBox(_sceneGroup, new Point3D(0, 0, 0), 4.5, 0.2, 3.0, bodyMat);

        // Screen
        var screenMat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(80, 20, 20, 50)));
        AddBox(_sceneGroup, new Point3D(0, 0.101, 0), 4.0, 0.01, 2.6, screenMat);

        // Edges
        var edgeMat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(60, 160, 170, 200)));
        AddBox(_sceneGroup, new Point3D(0, 0, 1.5), 4.5, 0.2, 0.04, edgeMat);
        AddBox(_sceneGroup, new Point3D(0, 0, -1.5), 4.5, 0.2, 0.04, edgeMat);
        AddBox(_sceneGroup, new Point3D(-2.25, 0, 0), 0.04, 0.2, 3.0, edgeMat);
        AddBox(_sceneGroup, new Point3D(2.25, 0, 0), 0.04, 0.2, 3.0, edgeMat);
    }

    private void BuildStudioShell()
    {
        // Display panel (tall, slightly tilted)
        var displayMat = CreateGlassMaterial(Color.FromArgb(40, 120, 130, 180));
        AddBox(_sceneGroup, new Point3D(0, 2.5, -0.3), 4.5, 3.0, 0.15, displayMat);

        // Screen
        var screenMat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(80, 20, 20, 50)));
        AddBox(_sceneGroup, new Point3D(0, 2.5, -0.22), 4.0, 2.6, 0.01, screenMat);

        // Stand
        var standMat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(60, 160, 170, 200)));
        AddBox(_sceneGroup, new Point3D(0, 1.2, 0.2), 0.3, 1.0, 0.08, standMat);

        // Base
        AddBox(_sceneGroup, new Point3D(0, 0, 0.5), 3.0, 0.15, 1.5, displayMat);
    }

    private void AddComponent3D(ThermalReadingViewModel reading, bool isLaptop)
    {
        var pos = GetComponent3DPosition(reading.ComponentName, isLaptop);
        var size = GetComponentSize(reading.ComponentName);
        var color = HeatColorConverter.GetHeatColor(reading.TemperatureCelsius);

        var mat = CreateComponentMaterial(color, reading.TemperatureCelsius);
        var model = new GeometryModel3D();

        if (IsRoundComponent(reading.ComponentName))
            model.Geometry = CreateSphere(16, 12);
        else
            model.Geometry = CreateBoxMesh(1, 1, 1);

        model.Material = mat;

        var tg = new Transform3DGroup();
        tg.Children.Add(new ScaleTransform3D(size.X, size.Y, size.Z));
        tg.Children.Add(new TranslateTransform3D(pos.X, pos.Y, pos.Z));
        model.Transform = tg;

        _sceneGroup.Children.Add(model);
        _componentModels[reading.ComponentName] = model;

        // Create 2D label
        var label = CreateLabel(reading);
        LabelOverlay.Children.Add(label);
        _componentLabels[reading.ComponentName] = label;
    }

    private static Point3D GetComponent3DPosition(string name, bool isLaptop)
    {
        if (isLaptop)
        {
            return name switch
            {
                "CPU" => new Point3D(-0.3, 0.08, -0.2),
                "GPU" => new Point3D(1.0, 0.08, -0.1),
                "SoC" => new Point3D(-0.3, 0.08, 0.4),
                "NPU" => new Point3D(-1.3, 0.08, 0.2),
                "SSD" => new Point3D(1.5, 0.08, 0.8),
                "Battery" => new Point3D(-0.8, 0.05, 1.2),
                "WiFi" => new Point3D(2.0, 0.08, -0.6),
                "Charger IC" => new Point3D(-2.0, 0.08, -0.2),
                "Modem" => new Point3D(2.0, 0.08, 0.3),
                "Display" => new Point3D(0, 1.7, -1.5),
                "Skin (Left)" => new Point3D(-2.2, 0.08, 0.5),
                "Skin (Right)" => new Point3D(2.2, 0.08, 0.5),
                "Skin (Back)" => new Point3D(0, 0.08, -0.8),
                "Skin (Bottom)" => new Point3D(0, -0.07, 0.5),
                "Skin (Front)" => new Point3D(0, 0.08, 1.8),
                "Thermal Policy" => new Point3D(0, 0.08, -0.5),
                "RAM" => new Point3D(0.6, 0.08, -0.4),
                _ => new Point3D(0, 0.2, 0)
            };
        }

        // Tablet / Surface Pro layout
        return name switch
        {
            "CPU" => new Point3D(-0.3, 0.05, -0.4),
            "GPU" => new Point3D(0.8, 0.05, -0.2),
            "SoC" => new Point3D(-0.3, 0.05, 0.2),
            "NPU" => new Point3D(-1.2, 0.05, 0),
            "SSD" => new Point3D(1.3, 0.05, 0.6),
            "Battery" => new Point3D(0, 0.03, 0.8),
            "WiFi" => new Point3D(1.6, 0.05, -0.8),
            "Charger IC" => new Point3D(-1.6, 0.05, -0.4),
            "Modem" => new Point3D(1.6, 0.05, 0.2),
            "Display" => new Point3D(0, 0.11, -0.3),
            "Skin (Left)" => new Point3D(-2.0, 0.05, 0),
            "Skin (Right)" => new Point3D(2.0, 0.05, 0),
            "Skin (Back)" => new Point3D(0, 0.05, -1.2),
            "Skin (Bottom)" => new Point3D(0, -0.08, 0),
            "Skin (Front)" => new Point3D(0, 0.05, 1.2),
            "RAM" => new Point3D(0.5, 0.05, -0.6),
            "PSU" => new Point3D(-1.0, 0.05, 0.5),
            _ => new Point3D(0, 0.15, 0)
        };
    }

    private static Vector3D GetComponentSize(string name)
    {
        return name switch
        {
            "CPU" => new Vector3D(0.55, 0.12, 0.55),
            "GPU" => new Vector3D(0.45, 0.10, 0.4),
            "SoC" => new Vector3D(0.35, 0.08, 0.35),
            "NPU" => new Vector3D(0.3, 0.08, 0.3),
            "SSD" => new Vector3D(0.5, 0.06, 0.25),
            "Battery" => new Vector3D(1.4, 0.06, 0.8),
            "WiFi" => new Vector3D(0.25, 0.06, 0.2),
            "Charger IC" => new Vector3D(0.25, 0.06, 0.2),
            "Modem" => new Vector3D(0.25, 0.06, 0.2),
            "Display" => new Vector3D(0.5, 0.5, 0.02),
            "RAM" => new Vector3D(0.4, 0.05, 0.15),
            "PSU" => new Vector3D(0.4, 0.15, 0.3),
            _ when name.StartsWith("Skin") => new Vector3D(0.2, 0.04, 0.2),
            "Thermal Policy" => new Vector3D(0.2, 0.04, 0.2),
            _ => new Vector3D(0.3, 0.08, 0.3)
        };
    }

    private static bool IsRoundComponent(string name)
    {
        return name is "CPU" or "GPU" or "SoC" or "NPU" or "WiFi" or "Modem"
            || name.StartsWith("Skin");
    }

    // ───────────────────────────────────────────────────────────
    //  Material Helpers
    // ───────────────────────────────────────────────────────────

    private static Material CreateGlassMaterial(Color color)
    {
        var group = new MaterialGroup();
        group.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
        group.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(60, 200, 220, 255)), 90));
        return group;
    }

    private static Material CreateComponentMaterial(Color heatColor, double temp)
    {
        var group = new MaterialGroup();
        // Base diffuse with heat color
        group.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(220, heatColor.R, heatColor.G, heatColor.B))));
        // Specular highlight
        group.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), 40));
        // Emissive glow proportional to temperature
        var glowAlpha = (byte)Math.Clamp((temp - 30) * 4, 0, 200);
        group.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(glowAlpha, heatColor.R, heatColor.G, heatColor.B))));
        return group;
    }

    // ───────────────────────────────────────────────────────────
    //  Update Methods
    // ───────────────────────────────────────────────────────────

    private void UpdateComponentColors()
    {
        if (DataContext is not MainViewModel vm)
            return;

        foreach (var reading in vm.Readings)
        {
            if (_componentModels.TryGetValue(reading.ComponentName, out var model))
            {
                var color = HeatColorConverter.GetHeatColor(reading.TemperatureCelsius);
                model.Material = CreateComponentMaterial(color, reading.TemperatureCelsius);
            }
            else
            {
                // New component — need to add it
                var isLaptop = vm.CurrentFormFactor == DeviceFormFactor.Laptop;
                AddComponent3D(reading, isLaptop);
            }
        }
    }

    private void UpdateLabels()
    {
        if (DataContext is not MainViewModel vm)
            return;

        foreach (var reading in vm.Readings)
        {
            if (_componentLabels.TryGetValue(reading.ComponentName, out var label))
            {
                UpdateLabelContent(label, reading);
            }
        }
    }

    // ───────────────────────────────────────────────────────────
    //  Geometry Builders
    // ───────────────────────────────────────────────────────────

    private static MeshGeometry3D CreateBoxMesh(double sx, double sy, double sz)
    {
        var mesh = new MeshGeometry3D();
        double hx = sx / 2, hy = sy / 2, hz = sz / 2;

        // 8 corners
        Point3D[] corners =
        {
            new(-hx, -hy, -hz), new( hx, -hy, -hz),
            new( hx,  hy, -hz), new(-hx,  hy, -hz),
            new(-hx, -hy,  hz), new( hx, -hy,  hz),
            new( hx,  hy,  hz), new(-hx,  hy,  hz),
        };

        // 6 faces (4 vertices each with proper normals)
        int[][] faces =
        {
            new[] {0,1,2,3}, // back  (-Z)
            new[] {5,4,7,6}, // front (+Z)
            new[] {4,0,3,7}, // left  (-X)
            new[] {1,5,6,2}, // right (+X)
            new[] {3,2,6,7}, // top   (+Y)
            new[] {4,5,1,0}, // bottom(-Y)
        };

        Vector3D[] normals =
        {
            new( 0, 0,-1), new( 0, 0, 1),
            new(-1, 0, 0), new( 1, 0, 0),
            new( 0, 1, 0), new( 0,-1, 0),
        };

        int idx = 0;
        for (int f = 0; f < 6; f++)
        {
            for (int v = 0; v < 4; v++)
            {
                mesh.Positions.Add(corners[faces[f][v]]);
                mesh.Normals.Add(normals[f]);
            }
            // Two triangles per face
            mesh.TriangleIndices.Add(idx);
            mesh.TriangleIndices.Add(idx + 1);
            mesh.TriangleIndices.Add(idx + 2);
            mesh.TriangleIndices.Add(idx);
            mesh.TriangleIndices.Add(idx + 2);
            mesh.TriangleIndices.Add(idx + 3);
            idx += 4;
        }
        return mesh;
    }

    private static MeshGeometry3D CreateSphere(int slices, int stacks)
    {
        var mesh = new MeshGeometry3D();
        double radius = 0.5;

        for (int stack = 0; stack <= stacks; stack++)
        {
            double phi = Math.PI * stack / stacks;
            double y = radius * Math.Cos(phi);
            double r = radius * Math.Sin(phi);

            for (int slice = 0; slice <= slices; slice++)
            {
                double theta = 2.0 * Math.PI * slice / slices;
                double x = r * Math.Cos(theta);
                double z = r * Math.Sin(theta);

                mesh.Positions.Add(new Point3D(x, y, z));
                mesh.Normals.Add(new Vector3D(x, y, z));
            }
        }

        for (int stack = 0; stack < stacks; stack++)
        {
            for (int slice = 0; slice < slices; slice++)
            {
                int a = stack * (slices + 1) + slice;
                int b = a + 1;
                int c = a + (slices + 1);
                int d = c + 1;

                mesh.TriangleIndices.Add(a);
                mesh.TriangleIndices.Add(c);
                mesh.TriangleIndices.Add(b);

                mesh.TriangleIndices.Add(b);
                mesh.TriangleIndices.Add(c);
                mesh.TriangleIndices.Add(d);
            }
        }
        return mesh;
    }

    private static void AddBox(Model3DGroup group, Point3D center, double sx, double sy, double sz, Material material)
    {
        var model = new GeometryModel3D
        {
            Geometry = CreateBoxMesh(1, 1, 1),
            Material = material,
            BackMaterial = material, // visible from inside (glass effect)
            Transform = new Transform3DGroup
            {
                Children =
                {
                    new ScaleTransform3D(sx, sy, sz),
                    new TranslateTransform3D(center.X, center.Y, center.Z)
                }
            }
        };
        group.Children.Add(model);
    }

    private void AddAngledLid(Model3DGroup group, Material material)
    {
        // Lid pivots from the hinge at the back of the base.
        // Transform order: Scale → shift hinge edge to origin → rotate up → translate to hinge position.
        var mesh = CreateBoxMesh(1, 1, 1);
        var model = new GeometryModel3D
        {
            Geometry = mesh,
            Material = material,
            BackMaterial = material,
            Transform = new Transform3DGroup
            {
                Children =
                {
                    new ScaleTransform3D(4.9, 0.08, 3.2),
                    // Shift so hinge edge (bottom of lid) sits at z=0; lid extends into -Z
                    new TranslateTransform3D(0, 0, -1.6),
                    // Rotate around X at origin (the hinge), lifting the lid upward
                    new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 70), new Point3D(0, 0, 0)),
                    // Move to hinge position (back edge of base)
                    new TranslateTransform3D(0, 0.15, -1.0)
                }
            }
        };
        group.Children.Add(model);
    }

    private void AddAngledScreen(Model3DGroup group, Material material)
    {
        // Screen sits on the inner face of the lid, same pivot and angle.
        var mesh = CreateBoxMesh(1, 1, 1);
        var model = new GeometryModel3D
        {
            Geometry = mesh,
            Material = material,
            BackMaterial = material,
            Transform = new Transform3DGroup
            {
                Children =
                {
                    new ScaleTransform3D(4.3, 0.01, 2.8),
                    // Center screen on lid (lid extends z=0 to z=-3.2, center=-1.6)
                    // Y offset = lid half-thickness (0.04) + screen half-thickness (0.005)
                    // — inner face (positive Y side before rotation = facing user after rotation)
                    new TranslateTransform3D(0, 0.045, -1.6),
                    // Same rotation as lid around the same hinge
                    new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 70), new Point3D(0, 0, 0)),
                    // Same hinge position
                    new TranslateTransform3D(0, 0.15, -1.0)
                }
            }
        };
        group.Children.Add(model);
    }

    private void AddGroundGrid()
    {
        // Subtle ground plane for spatial reference
        var mat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(15, 100, 120, 180)));
        AddBox(_sceneGroup, new Point3D(0, -0.15, 0.5), 8, 0.005, 6, mat);
    }

    // ───────────────────────────────────────────────────────────
    //  Labels (2D overlay projected from 3D)
    // ───────────────────────────────────────────────────────────

    private static Border CreateLabel(ThermalReadingViewModel reading)
    {
        var color = HeatColorConverter.GetHeatColor(reading.TemperatureCelsius);
        var isDark = Application.Current?.Resources["ThemeBgBrush"] is SolidColorBrush bg
                     && bg.Color.R + bg.Color.G + bg.Color.B < 384;
        var labelBg = isDark
            ? Color.FromArgb(200, 20, 20, 35)
            : Color.FromArgb(210, 245, 245, 250);
        var labelFg = isDark
            ? Color.FromArgb(220, 200, 210, 240)
            : Color.FromArgb(240, 30, 30, 50);

        var label = new Border
        {
            Background = new SolidColorBrush(labelBg),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5, 2, 5, 2),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = reading.Label.Length > 0 ? reading.Label : reading.ComponentName,
                        Foreground = new SolidColorBrush(labelFg),
                        FontSize = 9,
                        FontWeight = FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = reading.TemperatureCelsius > 0 ? reading.TemperatureDisplay : "—",
                        Foreground = new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B)),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            },
            Tag = reading.ComponentName,
            Visibility = Visibility.Collapsed
        };
        return label;
    }

    private static void UpdateLabelContent(Border label, ThermalReadingViewModel reading)
    {
        var color = HeatColorConverter.GetHeatColor(reading.TemperatureCelsius);
        label.BorderBrush = new SolidColorBrush(Color.FromArgb(150, color.R, color.G, color.B));

        var isDark = Application.Current?.Resources["ThemeBgBrush"] is SolidColorBrush bg
                     && bg.Color.R + bg.Color.G + bg.Color.B < 384;
        label.Background = new SolidColorBrush(isDark
            ? Color.FromArgb(200, 20, 20, 35)
            : Color.FromArgb(210, 245, 245, 250));

        if (label.Child is StackPanel sp && sp.Children.Count >= 2)
        {
            if (sp.Children[0] is TextBlock nameText)
            {
                nameText.Foreground = new SolidColorBrush(isDark
                    ? Color.FromArgb(220, 200, 210, 240)
                    : Color.FromArgb(240, 30, 30, 50));
            }
            if (sp.Children[1] is TextBlock tempText)
            {
                tempText.Text = reading.TemperatureCelsius > 0 ? reading.TemperatureDisplay : "—";
                tempText.Foreground = new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B));
            }
        }
    }

    // Called every frame to project 3D positions onto 2D overlay
    private void OnRender(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm || !IsVisible)
            return;

        var isLaptop = vm.CurrentFormFactor == DeviceFormFactor.Laptop;

        foreach (var reading in vm.Readings)
        {
            if (!_componentLabels.TryGetValue(reading.ComponentName, out var label))
                continue;

            var pos3D = GetComponent3DPosition(reading.ComponentName, isLaptop);
            // Offset label slightly above component
            pos3D.Y += GetComponentSize(reading.ComponentName).Y + 0.15;

            var point2D = Project3DTo2D(pos3D);
            if (point2D.HasValue)
            {
                label.Visibility = Visibility.Visible;
                Canvas.SetLeft(label, point2D.Value.X - 25);
                Canvas.SetTop(label, point2D.Value.Y - 30);
            }
            else
            {
                label.Visibility = Visibility.Collapsed;
            }
        }
    }

    private Point? Project3DTo2D(Point3D point3D)
    {
        var viewport = MainViewport;
        if (viewport.ActualWidth <= 0 || viewport.ActualHeight <= 0)
            return null;

        var camera = Camera;
        var matView = GetViewMatrix(camera);
        var matProj = GetProjectionMatrix(camera, viewport.ActualWidth / viewport.ActualHeight);

        // Transform point
        var p = matView.Transform(point3D);
        var p4 = matProj.Transform(new Point3D(p.X, p.Y, p.Z));

        // Check if behind camera
        // After projection, check if the point was behind near plane
        var transformed = new Point4D(p.X, p.Y, p.Z, 1);
        transformed = Multiply(matProj, transformed);

        if (transformed.W <= 0)
            return null;

        double ndcX = transformed.X / transformed.W;
        double ndcY = transformed.Y / transformed.W;

        // NDC to screen
        double screenX = (ndcX + 1.0) / 2.0 * viewport.ActualWidth;
        double screenY = (1.0 - ndcY) / 2.0 * viewport.ActualHeight;

        if (screenX < -50 || screenX > viewport.ActualWidth + 50 ||
            screenY < -50 || screenY > viewport.ActualHeight + 50)
            return null;

        return new Point(screenX, screenY);
    }

    private static Matrix3D GetViewMatrix(PerspectiveCamera camera)
    {
        var lookDir = camera.LookDirection;
        lookDir.Normalize();
        var up = camera.UpDirection;
        up.Normalize();

        var right = Vector3D.CrossProduct(lookDir, up);
        right.Normalize();
        up = Vector3D.CrossProduct(right, lookDir);
        up.Normalize();

        var pos = camera.Position;
        return new Matrix3D(
            right.X, up.X, -lookDir.X, 0,
            right.Y, up.Y, -lookDir.Y, 0,
            right.Z, up.Z, -lookDir.Z, 0,
            -Vector3D.DotProduct(right, (Vector3D)pos),
            -Vector3D.DotProduct(up, (Vector3D)pos),
            Vector3D.DotProduct(lookDir, (Vector3D)pos),
            1);
    }

    private static Matrix3D GetProjectionMatrix(PerspectiveCamera camera, double aspectRatio)
    {
        double fov = camera.FieldOfView * Math.PI / 180.0;
        double f = 1.0 / Math.Tan(fov / 2.0);
        double near = camera.NearPlaneDistance;
        double far = camera.FarPlaneDistance;

        return new Matrix3D(
            f / aspectRatio, 0, 0, 0,
            0, f, 0, 0,
            0, 0, far / (near - far), -1,
            0, 0, near * far / (near - far), 0);
    }

    private static Point4D Multiply(Matrix3D m, Point4D p)
    {
        return new Point4D(
            m.M11 * p.X + m.M21 * p.Y + m.M31 * p.Z + m.OffsetX * p.W,
            m.M12 * p.X + m.M22 * p.Y + m.M32 * p.Z + m.OffsetY * p.W,
            m.M13 * p.X + m.M23 * p.Y + m.M33 * p.Z + m.OffsetZ * p.W,
            m.M14 * p.X + m.M24 * p.Y + m.M34 * p.Z + m.M44 * p.W);
    }

    // ───────────────────────────────────────────────────────────
    //  Trackball Camera Controls
    // ───────────────────────────────────────────────────────────

    private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isRotating = true;
        _lastMousePos = e.GetPosition(MainViewport);
        MainViewport.CaptureMouse();
    }

    private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isRotating = false;
        MainViewport.ReleaseMouseCapture();
    }

    private void Viewport_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isPanning = true;
        _lastMousePos = e.GetPosition(MainViewport);
        MainViewport.CaptureMouse();
    }

    private void Viewport_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isPanning = false;
        MainViewport.ReleaseMouseCapture();
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(MainViewport);
        var dx = pos.X - _lastMousePos.X;
        var dy = pos.Y - _lastMousePos.Y;
        _lastMousePos = pos;

        if (_isRotating)
        {
            _rotationY += dx * 0.5;
            _rotationX += dy * 0.5;
            _rotationX = Math.Clamp(_rotationX, -89, 89);
            UpdateCamera();
        }
        else if (_isPanning)
        {
            // Pan: move lookAt point
            var radY = _rotationY * Math.PI / 180;
            var rightX = Math.Cos(radY);
            var rightZ = -Math.Sin(radY);
            var panSpeed = _distance * 0.002;

            _lookAt.X -= rightX * dx * panSpeed;
            _lookAt.Z -= rightZ * dx * panSpeed;
            _lookAt.Y += dy * panSpeed;
            UpdateCamera();
        }
    }

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _distance *= e.Delta > 0 ? 0.9 : 1.1;
        _distance = Math.Clamp(_distance, 3, 40);
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        var radX = _rotationX * Math.PI / 180;
        var radY = _rotationY * Math.PI / 180;

        double x = _distance * Math.Cos(radX) * Math.Sin(radY);
        double y = _distance * Math.Sin(radX);
        double z = _distance * Math.Cos(radX) * Math.Cos(radY);

        Camera.Position = new Point3D(_lookAt.X + x, _lookAt.Y + y, _lookAt.Z + z);
        Camera.LookDirection = new Vector3D(-x, -y, -z);
    }
}

// Helper struct for 4D point
internal struct Point4D
{
    public double X, Y, Z, W;
    public Point4D(double x, double y, double z, double w) { X = x; Y = y; Z = z; W = w; }
}
