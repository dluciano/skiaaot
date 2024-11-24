﻿using System.Diagnostics;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace SkiaAot;

internal class MainPage : ContentPage, IDisposable
{
    private readonly SKGLView canvas;
    private readonly Stopwatch stopwatch = new();

    private bool pageIsActive;
    private bool isDisposed;
    private IDispatcherTimer? animationTimer;
    private readonly SKRuntimeShaderBuilder builder;
    private readonly SKRuntimeEffect effect;

    // Use float arrays for shader uniforms to avoid creating new arrays repeatedly
    private readonly float[] iResolution = new float[3];
    private readonly float[] iMouse = new float[4];
    private float iTime;

    public MainPage()
    {
        if (Application.Current is null) throw new NullReferenceException("The current application is null.");
        
        // Initialize SKCanvasView with hardware acceleration
        canvas = new SKGLView()
        {
            EnableTouchEvents = true, // Enable touch for mouse input
        };
        
        canvas.PaintSurface += OnGLViewPaintSurface;
        canvas.Touch += OnGLViewTouch;

        Content = canvas;

        // Initialize shader
        var src = @"
uniform float3 iResolution;      // Viewport resolution (pixels)
uniform float  iTime;            // Shader playback time (s)
uniform float4 iMouse;           // Mouse drag pos=.xy Click pos=.zw (pixels)

const float cloudscale = 1.1;
const float speed = 0.03;
const float clouddark = 0.5;
const float cloudlight = 0.3;
const float cloudcover = 0.2;
const float cloudalpha = 8.0;
const float skytint = 0.5;
const vec3 skycolour1 = vec3(0.2, 0.4, 0.6);
const vec3 skycolour2 = vec3(0.4, 0.7, 1.0);

const mat2 m = mat2( 1.6,  1.2, -1.2,  1.6 );

vec2 hash( vec2 p ) {
    p = vec2(dot(p,vec2(127.1,311.7)), dot(p,vec2(269.5,183.3)));
    return -1.0 + 2.0*fract(sin(p)*43758.5453123);
}

float noise( in vec2 p ) {
    const float K1 = 0.366025404; // (sqrt(3)-1)/2;
    const float K2 = 0.211324865; // (3-sqrt(3))/6;
    vec2 i = floor(p + (p.x+p.y)*K1);    
    vec2 a = p - i + (i.x+i.y)*K2;
    vec2 o = (a.x>a.y) ? vec2(1.0,0.0) : vec2(0.0,1.0);
    vec2 b = a - o + K2;
    vec2 c = a - 1.0 + 2.0*K2;
    vec3 h = max(0.5-vec3(dot(a,a), dot(b,b), dot(c,c) ), 0.0 );
    vec3 n = h*h*h*h*vec3( dot(a,hash(i+0.0)), dot(b,hash(i+o)), dot(c,hash(i+1.0)));
    return dot(n, vec3(70.0));    
}

float fbm(vec2 n) {
    float total = 0.0, amplitude = 0.1;
    for (int i = 0; i < 7; i++) {
        total += noise(n) * amplitude;
        n = m * n;
        amplitude *= 0.4;
    }
    return total;
}
half4 main(in vec2 fragcoord) {
     // Normalized pixel coordinates (from -1 to 1)
    vec2 uv = (2. * fragcoord - iResolution.xy) / iResolution.y;

    float d = length(uv);
    vec3 col = vec3(.75, .50, .25) / d;
    col = sin(col*iTime);

    // Output to screen
    return vec4(col,1.0);
}
half4 beornottobemain(in vec2 fragCoord) {
    vec2 p = fragCoord.xy / iResolution.xy;
    vec2 uv = p*vec2(iResolution.x/iResolution.y,1.0);    
    float time = iTime * speed;
    float q = fbm(uv * cloudscale * 0.5);
    
    //ridged noise shape
    float r = 0.0;
    uv *= cloudscale;
    uv -= q - time;
    float weight = 0.8;
    for (int i=0; i<8; i++){
        r += abs(weight*noise( uv ));
        uv = m*uv + time;
        weight *= 0.7;
    }
    
    //noise shape
    float f = 0.0;
    uv = p*vec2(iResolution.x/iResolution.y,1.0);
    uv *= cloudscale;
    uv -= q - time;
    weight = 0.7;
    for (int i=0; i<8; i++){
        f += weight*noise( uv );
        uv = m*uv + time;
        weight *= 0.6;
    }
    
    f *= r + f;
    
    //noise colour
    float c = 0.0;
    time = iTime * speed * 2.0;
    uv = p*vec2(iResolution.x/iResolution.y,1.0);
    uv *= cloudscale*2.0;
    uv -= q - time;
    weight = 0.4;
    for (int i=0; i<7; i++){
        c += weight*noise( uv );
        uv = m*uv + time;
        weight *= 0.6;
    }
    
    //noise ridge colour
    float c1 = 0.0;
    time = iTime * speed * 3.0;
    uv = p*vec2(iResolution.x/iResolution.y,1.0);
    uv *= cloudscale*3.0;
    uv -= q - time;
    weight = 0.4;
    for (int i=0; i<7; i++){
        c1 += abs(weight*noise( uv ));
        uv = m*uv + time;
        weight *= 0.6;
    }
    
    c += c1;
    
    vec3 skycolour = mix(skycolour2, skycolour1, p.y);
    vec3 cloudcolour = vec3(1.1, 1.1, 0.9) * clamp((clouddark + cloudlight*c), 0.0, 1.0);
   
    f = cloudcover + cloudalpha*f*r;
    
    vec3 result = mix(skycolour, clamp(skytint * skycolour + cloudcolour, 0.0, 1.0), clamp(f + c, 0.0, 1.0));
    
    return vec4(result, 1.0);
}
";

        // Create shader effect and initialize uniforms
        this.effect = SKRuntimeEffect.CreateShader(src, out var errorText);
        if (!string.IsNullOrEmpty(errorText))
        {
            throw new Exception($"Shader compilation error: {errorText}");
        }

        builder = new SKRuntimeShaderBuilder(effect);

        // Initialize uniform values
        builder.Uniforms["iResolution"] = iResolution;
        builder.Uniforms["iMouse"] = iMouse;
        builder.Uniforms["iTime"] = iTime;
    }


    private void OnGLViewTouch(object? sender, SKTouchEventArgs e)
    {
        iMouse[2] = e.Location.X;
        iMouse[3] = e.Location.Y;

        if (e.ActionType == SKTouchAction.Pressed)
        {
            iMouse[0] = e.Location.X;
            iMouse[1] = e.Location.Y;
        }

        builder.Uniforms["iMouse"] = iMouse;
        e.Handled = true;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        pageIsActive = true;
        stopwatch.Start();

        // Use DispatcherTimer for more reliable animation timing
        animationTimer = Application.Current?.Dispatcher.CreateTimer();
        if (animationTimer != null)
        {
            animationTimer.Interval = TimeSpan.FromMilliseconds(16.666); // Target 60 FPS
            animationTimer.Tick += (s, e) =>
            {
                if (!pageIsActive) return;

                iTime = (float)stopwatch.Elapsed.TotalSeconds;
                builder.Uniforms["iTime"] = iTime;
                canvas.InvalidateSurface();
            };
            animationTimer.Start();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        pageIsActive = false;
        animationTimer?.Stop();
        stopwatch.Stop();
    }

    private void OnGLViewPaintSurface(object? sender, SKPaintGLSurfaceEventArgs args)
    {
        var info = args.BackendRenderTarget;
        var canvas = args.Surface.Canvas;
        var surface = args.Surface;

        // Update resolution if changed
        iResolution[0] = info.Width;
        iResolution[1] = info.Height;
        builder.Uniforms["iResolution"] = iResolution;

        // Create and use shader
        using var shader = builder.Build();
        using var paint = new SKPaint
        {
            Shader = shader,
        };

        // Draw shader
        canvas.Clear(SKColors.Black);
        canvas.DrawRect(SKRect.Create(info.Width, info.Height), paint);
    }

    public void Dispose()
    {
        if (isDisposed) return;

        animationTimer?.Stop();
        effect.Dispose();
        builder.Dispose();

        isDisposed = true;
        GC.SuppressFinalize(this);
    }
}