﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NEO_Block_API.RPC;
using NEO_Block_API.lib;
using Microsoft.AspNetCore.Cors;

namespace NEO_Block_API.Controllers
{
    //[RpcRoute("api/[controller]")]
    [Route("api/[controller]")]
    public class TestnetController : Controller
    {
        mongoHelper mh = new mongoHelper();

        private string formatTxid(string txid)
        {
            string result = txid.ToLower();
            if (result.Length == 64) {
                result = "0x" + result;
            }

            return result;
        }

        private JsonResult getRes(JsonRPCrequest req)
        {
            JArray result = new JArray();
            string findFliter = string.Empty;
            try
            {
                switch (req.method)
                {
                    case "getblockheight":
                        string resultStr = "[{blockheight:" + mh.GetBlockMaxIndex(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet) + "}]";
                        result = JArray.Parse(resultStr);
                        break;
                    case "getblock":
                        findFliter = "{index:" + req.@params[0] + "}";
                        result = mh.GetData(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "block", findFliter);
                        break;
                    case "gettransaction":
                        findFliter = "{txid:'" + formatTxid((string)req.@params[0]) + "'}";
                        result = mh.GetData(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "tx", findFliter);
                        break;
                    case "getasset":
                        findFliter = "{id:'" + formatTxid((string)req.@params[0]) + "'}";
                        result = mh.GetData(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "asset", findFliter);
                        break;
                    case "getfulllog":
                        findFliter = "{txid:'" + formatTxid((string)req.@params[0]) + "'}";
                        result = mh.GetData(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "fulllog", findFliter);
                        break;
                    case "getnotify":
                        findFliter = "{txid:'" + formatTxid((string)req.@params[0]) + "'}";
                        result = mh.GetData(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "notify", findFliter);
                        break;
                    case "getutxo":
                        if (req.@params.Count() == 1)
                        {
                            findFliter = "{addr:'" + req.@params[0] + "',used:''}";
                        };
                        if (req.@params.Count() == 2)
                        {
                            if ((Int64)req.@params[1] == 1)
                            {
                                findFliter = "{addr:'" + req.@params[0] + "'}";
                            }
                        }
                        result = mh.GetData(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "utxo", findFliter);
                        break;
                    case "getbalance":
                        findFliter = "{addr:'" + req.@params[0] + "',used:''}";
                        JArray utxos = mh.GetData(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "utxo", findFliter);
                        Dictionary<string, decimal> balance = new Dictionary<string, decimal>();
                        foreach (JObject j in utxos) {
                            if (!balance.ContainsKey((string)j["asset"]))
                            {
                                balance.Add((string)j["asset"], (decimal)j["value"]);
                            }
                            else
                            {
                                balance[(string)j["asset"]] += (decimal)j["value"];
                            }
                        }
                        JArray balanceJA = new JArray();
                        foreach (KeyValuePair<string,decimal> kv in balance)
                        {
                            JObject j = new JObject();
                            j.Add("asset",kv.Key);
                            j.Add("balance", kv.Value);
                            JObject asset = (JObject)mh.GetData(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "asset", "{id:'" + kv.Key + "'}")[0];
                            JArray name = (JArray)asset["name"];
                            j.Add("name", name);
                            balanceJA.Add(j);
                        }
                        result = balanceJA;
                        break;
                }
                if (result.Count == 0) {
                    JsonPRCresponse_Error resE = new JsonPRCresponse_Error();
                    resE.jsonrpc = "2.0";
                    resE.id = req.id;
                    resE.error.code = -1;
                    resE.error.message = "No Data";
                    resE.error.data = "Data does not exist";

                    return Json(resE);
                }
            }
            catch (Exception e)
            {
                JsonPRCresponse_Error resE = new JsonPRCresponse_Error();
                resE.jsonrpc = "2.0";
                resE.id = 0;
                resE.error.code = -100;
                resE.error.message = "Parameter Error";
                resE.error.data = e.Message;

                return Json(resE);

            }

            JsonPRCresponse res = new JsonPRCresponse();
            res.jsonrpc = req.jsonrpc;
            res.id = req.id;
            res.result = result;

            return Json(res);
        }

        [HttpGet]
        public JsonResult Get(string @jsonrpc, string @method, string @params, long @id)
        {

            try
            {
                JsonRPCrequest req = new JsonRPCrequest
                {
                    jsonrpc = @jsonrpc,
                    method = @method,
                    @params = JsonConvert.DeserializeObject<object[]>(JsonConvert.SerializeObject(JArray.Parse(@params))),
                    id = @id
                };

                return getRes(req);
            }
            catch (Exception e)
            {
                JsonPRCresponse_Error resE = new JsonPRCresponse_Error();
                resE.jsonrpc = "2.0";
                resE.id = 0;
                resE.error.code = -100;
                resE.error.message = "Parameter Error";
                resE.error.data = e.Message;

                return Json(resE);

            }
        }

        [HttpPost]
        public JsonResult Post([FromBody]JsonRPCrequest req)
        {
            return getRes(req);
        }

    }
}