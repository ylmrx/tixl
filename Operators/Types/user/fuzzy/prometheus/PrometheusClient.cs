using System;
using T3.Core.Logging;
using T3.Core.Operator;
using T3.Core.Operator.Attributes;
using T3.Core.Operator.Slots;
using System.Net.Http;
using T3.Core.Utils;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;

namespace T3.Operators.Types.Id_af8bb80b_e685_4985_a1ad_625629146b04
{
    public class PrometheusClient : Instance<PrometheusClient>
    { 
        [Output(Guid = "d6007dab-7016-433b-8ad7-6612710c3aef")]
        public readonly Slot<List<float>> timestamps = new Slot<List<float>>();

        [Output(Guid = "5dd894cf-68c0-49bf-b67d-18924b0f443d")]
        public readonly Slot<List<float>> values = new Slot<List<float>>();

        public enum PrometheusRequestType {
            Instant,
            Range
        }

        public PrometheusClient() {
            timestamps.UpdateAction = Update;
            values.UpdateAction = Update;
        }

        private void Update(EvaluationContext context) {
            var prometheusValue = prometheus.GetValue(context);
            var queryValue = query.GetValue(context);
            var endValue = end.GetValue(context);
            var startValue = start.GetValue(context);
            var stepValue = step.GetValue(context);
            var timeoutValue = timeout.GetValue(context);
            var typeValue = type.GetValue(context);

            var isPDirty = prometheus.DirtyFlag.IsDirty;
            var isQDirty = query.DirtyFlag.IsDirty;
            var isEDirty = end.DirtyFlag.IsDirty;
            var isSDirty = start.DirtyFlag.IsDirty;
            var isStDirty = step.DirtyFlag.IsDirty;

            var wasTriggered = MathUtils.WasTriggered(triggerFetch.GetValue(context), ref _triggered);

            var to = timeoutValue.Equals("") ? "" : "&timeout=" + timeoutValue;

            if (wasTriggered || isPDirty || isQDirty || isEDirty || isSDirty || isStDirty) {
                if (typeValue == (int)PrometheusRequestType.Instant)
                {
                    FetchUrl(prometheusValue + "api/v1/query?query=" + queryValue + to);
                }
                if (typeValue == (int)PrometheusRequestType.Range)
                {
                    if(endValue == 0 || startValue == 0 || stepValue.Equals("")) {
                        Log.Warning("we need both 'start' and 'end' and 'step' set when in range mode");
                    }
                    else {
                        FetchUrl(prometheusValue + "api/v1/query_range?query=" + queryValue +
                            "&start=" + startValue.ToString() + "&end=" + endValue.ToString() +
                            "&step=" + stepValue + to);
                    }
                }
            }
        }
        
        private bool _triggered;

        private async void FetchUrl(string uri) {
            try {
                var client = new HttpClient(new HttpClientHandler() { UseDefaultCredentials = true });
                var req = new HttpRequestMessage {
                    RequestUri = new Uri(uri),
                    Method = HttpMethod.Get,
                };
                var httpResponseTask = await client.SendAsync(req);
                var res = await httpResponseTask.Content.ReadAsStringAsync();;
                var m = JsonNode.Parse(res);
                
                System.Collections.Generic.List<float> tsOut = new();
                System.Collections.Generic.List<float> valOut = new();
                if (m["status"].ToJsonString().Equals("\"success\"")) {
                    if (m["data"]["resultType"].ToJsonString().Equals("\"matrix\"")) {
                        var jsDoc = JsonDocument.Parse(m["data"]["result"][0]["values"].ToJsonString());
                        foreach (var elem in jsDoc.RootElement.EnumerateArray()) {
                            tsOut.Add(float.Parse(elem[0].ToString()));
                            valOut.Add(float.Parse(elem[1].ToString()));
                        }
                        timestamps.Value = tsOut;
                        values.Value = valOut;
                    } else {
                        tsOut.Add(float.Parse(m["data"]["result"][0]["value"][0].ToJsonString()));
                        valOut.Add(float.Parse(m["data"]["result"][0]["value"][1].ToJsonString().Trim('"')));
                        timestamps.Value = tsOut;
                        values.Value = valOut;
                    }
                }
            }
            catch(Exception e) {
                Log.Warning("failed: " + e.Message);
            }
        }

        [Input(Guid = "155fa972-e80a-4c8e-9958-9041a7c0616d")]
        public readonly InputSlot<string> query = new InputSlot<string>();

        [Input(Guid = "b8ca1dde-f912-4269-bd0b-35182c4d6ab1")]
        public readonly InputSlot<string> prometheus = new InputSlot<string>();

        [Input(Guid = "7a4472eb-769f-4806-9d98-75f1ebe8b19a")]
        public readonly InputSlot<bool> triggerFetch = new InputSlot<bool>();

        [Input(Guid = "8ad8064b-4e80-428c-9e41-92c60277ec78", MappedType = typeof(PrometheusRequestType))]
        public readonly InputSlot<int> type = new();

        [Input(Guid = "a149c51f-24fc-47f3-ad47-af6510c08e42")]
        public readonly InputSlot<int> start = new InputSlot<int>();

        [Input(Guid = "48751360-8a93-4c19-a1bb-e5ef6ea32b6e")]
        public readonly InputSlot<int> end = new InputSlot<int>();

        [Input(Guid = "880dd1f9-5053-45b4-9d44-e2e9baca60f1")]
        public readonly InputSlot<string> step = new InputSlot<string>();

        [Input(Guid = "dbb8ec7a-a9cd-4a2d-b351-cedb9e966507")]
        public readonly InputSlot<string> timeout = new InputSlot<string>();

    }
}

