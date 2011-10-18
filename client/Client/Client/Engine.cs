﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Graphics;
using System.Windows.Input;

using Client.Diagnostics;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Client
{
    public class Engine
    {
        private readonly TimeSpan _maximumElapsedTime = TimeSpan.FromMilliseconds(500.0);
        private readonly GameServiceContainer _gameServices;
        private readonly DrawState _drawState;
        private readonly UpdateState _updateState;
        private readonly DrawingSurface _drawingSurface;

        private int _updatesSinceRunningSlowly1 = int.MaxValue;
        private int _updatesSinceRunningSlowly2 = int.MaxValue;

        private bool _drawRunningSlowly;
        private bool _doneFirstUpdate;
        private bool _forceElapsedTimeToZero;
        private bool _suppressDraw;

        private TimeSpan _totalGameTime;
        private TimeSpan _targetElapsedTime;
        private TimeSpan _accumulatedElapsedGameTime;
        private TimeSpan _lastFrameElapsedGameTime;

        private ContentManager _content;

        public DrawingSurface DrawingSurface
        {
            get { return _drawingSurface; }
        }

        public GameServiceContainer Services
        {
            get { return _gameServices; }
        }

        public TimeSpan TargetElapsedTime
        {
            get { return _targetElapsedTime; }
            set
            {
                Asserter.AssertIsNotLessThan("value", value, TimeSpan.Zero);
                _targetElapsedTime = value;
            }
        }

        public GraphicsDevice GraphicsDevice
        {
            get { return GraphicsDeviceManager.Current.GraphicsDevice; }
        }

        public ContentManager Content
        {
            get { return _content; }
            set
            {
                Asserter.AssertIsNotNull(value, "value");
                _content = value;
            }
        }

        public Control RootControl
        {
            get;
            private set;
        }

        public Engine(DrawingSurface drawingSurface)
        {
            Asserter.AssertIsNotNull(drawingSurface, "drawingSurface");

            _drawingSurface = drawingSurface;
            _drawingSurface.SizeChanged += OnDrawingSurfaceSizeChanged;
            _content = new ContentManager(_gameServices)
            {                
                RootDirectory = "Content"
            };
            _totalGameTime = TimeSpan.Zero;
            _accumulatedElapsedGameTime = TimeSpan.Zero;
            _lastFrameElapsedGameTime = TimeSpan.Zero;
            _targetElapsedTime = TimeSpan.FromTicks(166667L);
            _drawState = new DrawState();
            _updateState = new UpdateState();
            _gameServices = new GameServiceContainer();

            RootControl = (Control)drawingSurface.Parent;
            Asserter.AssertIsNotNull(RootControl, "RootControl");
        }

        ~Engine()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void ResetElapsedTime()
        {
            Tracer.Info("ResetElapsedTime was called");

            _forceElapsedTimeToZero = true;
            _drawRunningSlowly = false;
            _updatesSinceRunningSlowly1 = int.MaxValue;
            _updatesSinceRunningSlowly2 = int.MaxValue;
        }

        public void Run()
        {
            try
            {
                Tracer.Info("Initializing Engine...");
                Initialize();

                _updateState.ElapsedGameTime = TimeSpan.Zero;
                _updateState.TotalGameTime = _totalGameTime;
                _updateState.IsRunningSlowly = false;

                Update(_updateState);

                _doneFirstUpdate = true;
            }
            catch (Exception e)
            {
                Tracer.Error(e);
                throw;
            }
        }

        public void Tick(TimeSpan elapsedTime, TimeSpan totalGameTime)
        {
            bool suppressDraw = true;

            if (elapsedTime < TimeSpan.Zero)
                elapsedTime = TimeSpan.Zero;

            if (_forceElapsedTimeToZero)
            {
                elapsedTime = TimeSpan.Zero;
                _forceElapsedTimeToZero = false;
            }

            if (elapsedTime > _maximumElapsedTime)
                elapsedTime = _maximumElapsedTime;

            if (Math.Abs(elapsedTime.Ticks - _targetElapsedTime.Ticks) < _targetElapsedTime.Ticks >> 6)
                elapsedTime = _targetElapsedTime;

            _accumulatedElapsedGameTime += elapsedTime;

            long speed = _accumulatedElapsedGameTime.Ticks / _targetElapsedTime.Ticks;

            _accumulatedElapsedGameTime = TimeSpan.FromTicks(_accumulatedElapsedGameTime.Ticks % _targetElapsedTime.Ticks);
            _lastFrameElapsedGameTime = TimeSpan.Zero;

            TimeSpan targetElapsedTime = _targetElapsedTime;

            if (speed > 1L)
            {
                _updatesSinceRunningSlowly2 = _updatesSinceRunningSlowly1;
                _updatesSinceRunningSlowly1 = 0;
            }
            else
            {
                if (_updatesSinceRunningSlowly1 < int.MaxValue)
                    ++_updatesSinceRunningSlowly1;
                if (_updatesSinceRunningSlowly2 < int.MaxValue)
                    ++_updatesSinceRunningSlowly2;
            }

            _drawRunningSlowly = _updatesSinceRunningSlowly2 < 20;

            while (speed > 0L)
            {
                --speed;
                try
                {
                    _updateState.ElapsedGameTime = targetElapsedTime;
                    _updateState.TotalGameTime = _totalGameTime;
                    _updateState.IsRunningSlowly = _drawRunningSlowly;
                    Update(_updateState);
                    suppressDraw = suppressDraw & _suppressDraw;
                    _suppressDraw = false;
                }
                finally
                {
                    _lastFrameElapsedGameTime += targetElapsedTime;
                    _totalGameTime = totalGameTime;
                }
            }

            if (!suppressDraw)
                DrawFrame();
        }

        protected virtual void Initialize()
        {
            Tracer.Info("Loading Content...");
            LoadContent();
        }

        protected virtual void Update(UpdateState state)
        {
            _doneFirstUpdate = true;
        }

        protected virtual bool BeginDraw()
        {
            return true;
        }

        protected virtual void Draw(DrawState state) { }

        protected virtual void EndDraw() { }

        protected virtual void LoadContent() { }

        protected virtual void UnloadContent() { }

        protected virtual void OnDrawingSurfaceSizeChanged(object sender, SizeChangedEventArgs e) { }

        protected virtual void OnMouseWheel(object sender, MouseWheelEventArgs e) { }

        protected virtual void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e) { }

        protected virtual void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e) { }

        protected virtual void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) { }

        protected virtual void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) { }

        protected virtual void OnMouseLeave(object sender, MouseEventArgs e) { }

        protected virtual void OnMouseEnter(object sender, MouseEventArgs e) { }

        protected virtual void OnKeyUp(object sender, KeyEventArgs e) { }

        protected virtual void OnKeyDown(object sender, KeyEventArgs e) { }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (this)
                {
                    Tracer.Info("Unloading Content...");
                    UnloadContent();

                    if (_drawingSurface != null)
                        _drawingSurface.SizeChanged -= OnDrawingSurfaceSizeChanged;
                }
            }
        }

        private void DrawFrame()
        {
            try
            {
                if (_doneFirstUpdate && (BeginDraw()))
                {
                    _drawState.TotalGameTime = _totalGameTime;
                    _drawState.ElapsedGameTime = _lastFrameElapsedGameTime;
                    _drawState.IsRunningSlowly = _drawRunningSlowly;
                    _drawState.GraphicsDevice = GraphicsDevice;

                    Draw(_drawState);
                    EndDraw();
                }
            }
            finally
            {
                _lastFrameElapsedGameTime = TimeSpan.Zero;
            }
        }
    }
}