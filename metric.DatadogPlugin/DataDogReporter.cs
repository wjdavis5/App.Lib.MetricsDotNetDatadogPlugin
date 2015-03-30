﻿using metric.DatadogPlugin.Interfaces;
using metric.DatadogPlugin.Models;
using metric.DatadogPlugin.Models.Metrics;
using metric.DatadogPlugin.Models.Transport;
using metrics;
using metrics.Core;
using metrics.Reporting;
using NUnit.Framework;
using StatsdClient;
using System;
using System.Collections.Generic;

/**
 * This code is a C# translation of https://github.com/coursera/metrics-datadog
 * built to work with the C# translation of metrics https://github.com/danielcrenna/metrics-net
 * 
 */
namespace metric.DatadogPlugin
{
    public class DataDogReporter : ReporterBase
    {

        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger("DataDogReporter");
        public const string ENVIRONMENT_TAG = "environment";
        public const string HOST_TAG = "host";

        private readonly DateTime unixOffset = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
        private readonly Metrics _metrics;
        private readonly IDictionary<string, string> _globalTags;
        private readonly double[] histogramPercentages = { 0.75, 0.95, 0.98, 0.99, 0.999 };
        private readonly ITransport transport;
        private readonly string[] path;
        private readonly IMetricNameFormatter formatter;

        public DataDogReporter(Metrics metrics, ITransport transport, IMetricNameFormatter formatter, IDictionary<string, string> globalTags, string[] path)
            : base(new TextMessageWriter(), metrics)
        {
            this._metrics = metrics;
            this._globalTags = globalTags;
            this.path = path;
            this.transport = transport;
            this.formatter = formatter;

        }

        public DataDogReporter(Metrics metrics, ITransport transport, IMetricNameFormatter formatter, string environment, string host, string[] path)
            : base(new TextMessageWriter(), metrics)
        {
            this._metrics = metrics;
            this.path = path;
            this.transport = transport;
            this.formatter = formatter;
            this._globalTags = new Dictionary<string, string>();
            _globalTags.Add(ENVIRONMENT_TAG, environment);
            _globalTags.Add(HOST_TAG, host);

        }

        public override void Run()
        {
            IRequest request = this.transport.Prepare();

            long timestamp = (long)(DateTime.UtcNow.Subtract(unixOffset).TotalSeconds);

            TransformMetrics(request, _metrics, timestamp);
        }

        /**
         * Broken out from the Run() method for unit testing
         */
        public IRequest TransformMetrics(IRequest request, Metrics metrics, long timestamp) 
        {
            foreach (var dictEntry in metrics.All)
            {
                if (dictEntry.Value is CounterMetric)
                {
                    LogCounter(request, dictEntry.Key, (CounterMetric)dictEntry.Value, timestamp);
                }
                else if (dictEntry.Value is HistogramMetric)
                {
                    LogHistogram(request, dictEntry.Key, (HistogramMetric)dictEntry.Value, timestamp);
                }
                else if (dictEntry.Value is MeterMetric)
                {
                    LogMeter(request, dictEntry.Key, (MeterMetric)dictEntry.Value, timestamp);
                }
                else if (dictEntry.Value is TimerMetric)
                {
                    LogTimer(request, dictEntry.Key, (TimerMetric)dictEntry.Value, timestamp);
                }
                else if (dictEntry.Value is GaugeMetric)
                {
                    LogGauge(request, dictEntry.Key, (GaugeMetric)dictEntry.Value, timestamp);
                }
                else
                {
                    Log.InfoFormat("Unknown metric type {}, not sending", dictEntry.Value.GetType());
                }
            }
            return request;
        }

        private void LogTimer(IRequest request, MetricName metricName, TimerMetric metric, long timestamp)
        {
            LogGauge(request, metricName.Name + ".FifteenMinuteRate", metric.FifteenMinuteRate, timestamp);
            LogGauge(request, metricName.Name + ".FiveMinuteRate", metric.FiveMinuteRate, timestamp);
            LogGauge(request, metricName.Name + ".OneMinuteRate", metric.OneMinuteRate, timestamp);
            LogGauge(request, metricName.Name + ".Max", metric.Max, timestamp);
            LogGauge(request, metricName.Name + ".Mean", metric.Mean, timestamp);
            LogGauge(request, metricName.Name + ".MeanRate", metric.MeanRate, timestamp);
            LogGauge(request, metricName.Name + ".Min", metric.Min, timestamp);
            LogGauge(request, metricName.Name + ".StdDev", metric.StdDev, timestamp);
        }

        private void LogMeter(IRequest request, MetricName metricName, MeterMetric metric, long timestamp)
        {
            request.AddCounter(new DatadogCounter(formatter.Format(metricName.Name, path), metric.Count, timestamp, _globalTags));
        }

        private void LogCounter(IRequest request, MetricName metricName, CounterMetric metric, long timestamp)
        {
            request.AddCounter(new DatadogCounter(formatter.Format(metricName.Name, path), metric.Count, timestamp, _globalTags));
        }

        private void LogHistogram(IRequest request, MetricName metricName, HistogramMetric metric, long timestamp)
        {
            LogGauge(request, metricName.Name + ".Max", metric.SampleMax, timestamp);
            LogGauge(request, metricName.Name + ".Min", metric.SampleMin, timestamp);
            LogGauge(request, metricName.Name + ".Mean", metric.SampleMean, timestamp);
            LogGauge(request, metricName.Name + ".StdDev", metric.StdDev, timestamp);
            LogGauge(request, metricName.Name + ".Count", metric.SampleCount, timestamp);

            double[] percentResults = metric.Percentiles(histogramPercentages);
            LogGauge(request, metricName.Name + ".75Percent", percentResults[0], timestamp);
            LogGauge(request, metricName.Name + ".95Percent", percentResults[1], timestamp);
            LogGauge(request, metricName.Name + ".98Percent", percentResults[2], timestamp);
            LogGauge(request, metricName.Name + ".99Percent", percentResults[3], timestamp);
            LogGauge(request, metricName.Name + ".999Percent", percentResults[4], timestamp);

            metric.Clear();

        }

        private void LogGauge(IRequest request, MetricName metricName, GaugeMetric metric, long timestamp)
        {
            LogGauge(request, metricName.Name, System.Convert.ToInt64(metric.ValueAsString), timestamp);
        }

        private void LogGauge(IRequest request, string metricName, double value, long timestamp)
        {
            request.AddGauge(new DatadogGauge(formatter.Format(metricName, path), value, timestamp, _globalTags));
        }
       
    }

}
