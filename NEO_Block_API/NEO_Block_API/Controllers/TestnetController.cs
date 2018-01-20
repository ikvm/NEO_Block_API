﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NEO_Block_API.RPC;
using NEO_Block_API.lib;
using Newtonsoft.Json.Linq;
using System.Net;
using System.IO;

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
            if (result.Length == 64)
            {
                result = "0x" + result;
            }

            return result;
        }

        private static string getTransferTxHex(string addrOut, string addrIn, string assetID, decimal amounts)
        {
            mongoHelper mh = new mongoHelper();

            ThinNeo.Transaction lastTran;

            string findFliter = "{addr:'" + addrOut + "',used:''}";
            JArray outputJA = mh.GetData(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "utxo", findFliter);

            //linq查找指定asset最大的utxo
            var query = from utxos in outputJA.Children()
                        where (string)utxos["asset"] == assetID
                        orderby (decimal)utxos["value"] descending
                        select utxos;
            var utxo = query.ToList()[0];

            byte[] utxo_txid = ThinNeo.Helper.HexString2Bytes(((string)utxo["txid"]).Replace("0x", "")).Reverse().ToArray();
            ushort utxo_n = (ushort)utxo["n"];
            decimal utxo_value = (decimal)utxo["value"];
            byte[] assetBytes = ThinNeo.Helper.HexString2Bytes(assetID.Replace("0x", "")).Reverse().ToArray();

            if (amounts > utxo_value)
            {
                return string.Empty;
            }

            lastTran = new ThinNeo.Transaction
            {
                type = ThinNeo.TransactionType.ContractTransaction,//转账
                attributes = new ThinNeo.Attribute[0],
                inputs = new ThinNeo.TransactionInput[1]
            };
            lastTran.inputs[0] = new ThinNeo.TransactionInput
            {
                hash = utxo_txid,//用掉指定asset最大的utxo
                index = utxo_n
            };

            lastTran.outputs = new ThinNeo.TransactionOutput[2];
            lastTran.outputs[0] = new ThinNeo.TransactionOutput
            {
                assetId = assetBytes,
                toAddress = ThinNeo.Helper.GetPublicKeyHashFromAddress(addrIn),
                value = amounts
            };//给对方转账
            lastTran.outputs[1] = new ThinNeo.TransactionOutput
            {
                assetId = assetBytes,
                toAddress = ThinNeo.Helper.GetPublicKeyHashFromAddress(addrOut),
                value = utxo_value - amounts
            };//给自己找零
            using (var ms = new System.IO.MemoryStream())
            {
                lastTran.SerializeUnsigned(ms);
                return ThinNeo.Helper.Bytes2HexString(ms.ToArray());
            }
        }

        private static JArray sendTxPlusSign(string txScriptHex, string signHex, string publicKeyHex)
        {
            byte[] txScript = ThinNeo.Helper.HexString2Bytes(txScriptHex);
            byte[] sign = ThinNeo.Helper.HexString2Bytes(signHex);
            byte[] pubkey = ThinNeo.Helper.HexString2Bytes(publicKeyHex);
            //byte[] prikey = privateKeyHex.HexToBytes();

            //byte[] sign = null;

            //sign = ThinNeo.Helper.Sign(txScript, prikey);

            //var pubkey = ThinNeo.Helper.GetPublicKeyFromPrivateKey(prikey);

            var addr = ThinNeo.Helper.GetAddressFromPublicKey(pubkey);

            ThinNeo.Transaction lastTran = new ThinNeo.Transaction();
            lastTran.Deserialize(new MemoryStream(txScript));
            lastTran.witnesses = null;
            lastTran.AddWitness(sign, pubkey, addr);

            string TxPlusSignStr = string.Empty;
            using (var ms = new System.IO.MemoryStream())
            {
                lastTran.Serialize(ms);
                TxPlusSignStr = ThinNeo.Helper.Bytes2HexString(ms.ToArray());
            }

            httpHelper hh = new httpHelper();
            var resp = hh.Post("http://47.96.168.8:20332", "{'jsonrpc':'2.0','method':'sendrawtransaction','params':['" + TxPlusSignStr +"'],'id':1}", System.Text.Encoding.UTF8, 1);

            return new JArray
                        {
                            new JObject
                            {
                                {
                                    "sendrawtransactionresult",
                                    (bool)JObject.Parse(resp)["result"]
                                }
                            }
                        };
        }

        private JsonResult getRes(JsonRPCrequest req)
        {
            JArray result = new JArray();
            string resultStr = string.Empty;
            string findFliter = string.Empty;
            string sortStr = string.Empty;
            try
            {
                switch (req.method)
                {
                    case "getnoderpcapi":
                        JArray JA = new JArray
                        {
                            new JObject {
                                { "nodeType","testnet" },
                                { "nodeList",new JArray{
                                    "47.96.168.8:20332"}
                                }
                            }
                        };
                        result = JA;
                        break;
                    case "getdatablockheight":
                        result = mh.Getdatablockheight(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet);
                        break;
                    case "getblockcount":
                        resultStr = "[{blockcount:" + mh.GetDataCount(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet,"block") + "}]";
                        result = JArray.Parse(resultStr);
                        break;
                    case "gettxcount":
                        resultStr = "[{txcount:" + mh.GetDataCount(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet,"tx") + "}]";
                        result = JArray.Parse(resultStr);
                        break;
                    case "getaddrcount":
                        resultStr = "[{addrcount:" + mh.GetDataCount(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "address") + "}]";
                        result = JArray.Parse(resultStr);
                        break;
                    case "getblock":
                        findFliter = "{index:" + req.@params[0] + "}";
                        result = mh.GetData(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "block", findFliter);
                        break;
                    case "getblocks":
                        sortStr = "{index:-1}";
                        result = mh.GetDataPages(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "block", sortStr,int.Parse(req.@params[0].ToString()), int.Parse(req.@params[1].ToString()));
                        break;
                    case "getrawtransaction":
                        findFliter = "{txid:'" + formatTxid((string)req.@params[0]) + "'}";
                        result = mh.GetData(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "tx", findFliter);
                        break;
                    case "getrawtransactions":
                        sortStr = "{blockindex:-1,txid:-1}";
                        findFliter = "{}";
                        if (req.@params.Count() > 2)
                        {
                            string txType = req.@params[2].ToString();

                            if (txType != null && txType != string.Empty)
                            { findFliter = "{type:'" + txType + "'}"; }
                        }
                        result = mh.GetDataPages(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "tx", sortStr, int.Parse(req.@params[0].ToString()), int.Parse(req.@params[1].ToString()),findFliter);
                        break;
                    case "getaddrs":
                        sortStr = "{'lastuse.blockindex' : -1,'lastuse.txid' : -1}";
                        result = mh.GetDataPages(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "address", sortStr, int.Parse(req.@params[0].ToString()), int.Parse(req.@params[1].ToString()));
                        break;
                    case "getasset":
                        findFliter = "{id:'" + formatTxid((string)req.@params[0]) + "'}";
                        result = mh.GetData(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "asset", findFliter);
                        break;
                    case "getallasset":
                        findFliter = "{}";
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
                        foreach (JObject j in utxos)
                        {
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
                        foreach (KeyValuePair<string, decimal> kv in balance)
                        {
                            JObject j = new JObject();
                            j.Add("asset", kv.Key);
                            j.Add("balance", kv.Value);
                            JObject asset = (JObject)mh.GetData(mh.mongodbConnStr_testnet, mh.mongodbDatabase_testnet, "asset", "{id:'" + kv.Key + "'}")[0];
                            JArray name = (JArray)asset["name"];
                            j.Add("name", name);
                            balanceJA.Add(j);
                        }
                        result = balanceJA;
                        break;
                    case "getcontractscript":
                        findFliter = "{hash:'" + (string)req.@params[0] + "'}";
                        result = mh.GetData(mh.mongodbConnStr_NeonOnline, mh.mongodbDatabase_NeonOnline, "contractWarehouse", findFliter);
                        break;
                    case "gettransfertxhex":
                        result = new JArray
                        {
                            new JObject
                            {
                                {
                                    "transfertxhex",
                                    getTransferTxHex((string)req.@params[0], (string)req.@params[1], (string)req.@params[2], decimal.Parse(req.@params[3].ToString()))
                                }
                            }
                        };
                        break;
                    case "sendtxplussign":
                        result = sendTxPlusSign((string)req.@params[0], (string)req.@params[1], (string)req.@params[2]);
                        //new JArray
                        //    {
                        //        new JObject
                        //        {
                        //            {
                        //                "sendtxplussign",
                        //                sendTxPlusSign((string)req.@params[0], (string)req.@params[1], (string)req.@params[2])
                        //            }
                        //        }
                        //    };
                        break;
                    case "sendrawtransaction":
                        httpHelper hh = new httpHelper();
                        var resp = hh.Post("http://47.96.168.8:20332", "{'jsonrpc':'2.0','method':'sendrawtransaction','params':['" + (string)req.@params[0] + "'],'id':1}", System.Text.Encoding.UTF8, 1);

                        result = new JArray
                        {
                            new JObject
                            {
                                {
                                    "sendrawtransactionresult",
                                    (bool)JObject.Parse(resp)["result"]
                                }
                            }
                        };
                        break;
                    case "setcontractscript":
                        string ipAddr = Request.HttpContext.Connection.RemoteIpAddress.ToString();
                        JObject J = JObject.Parse((string)req.@params[0]);
                        string hash = (string)J["hash"];
                        //string hash = (string)req.@params[0];
                        //J.Add("hash", hash);
                        //J.Add("avm", (string)req.@params[1]);
                        //J.Add("cs", (string)req.@params[2]);

                        //string mapStr = (string)req.@params[3];
                        //string abiStr = (string)req.@params[4];

                        //if (mapStr != null && mapStr != string.Empty)
                        //{
                        //    J.Add("map", JArray.Parse((string)req.@params[3]));
                        //}
                        //else
                        //{
                        //    J.Add("map", string.Empty);
                        //}

                        //if (abiStr != null && abiStr != string.Empty)
                        //{
                        //    J.Add("abi", JObject.Parse((string)req.@params[4]));
                        //}
                        //else
                        //{
                        //    J.Add("abi", string.Empty);
                        //}

                        J.Add("requestIP", ipAddr);

                        mh.InsertOneDataByCheckKey(mh.mongodbConnStr_NeonOnline, mh.mongodbDatabase_NeonOnline, "contractWarehouse", J,"hash", hash);
                        result = new JArray
                        {
                            new JObject{
                                { "isSetSuccess",true }
                            }
                        };

                        break;
                }
                if (result.Count == 0)
                {
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
        public async Task<JsonResult> Post()
        {
            var ctype = HttpContext.Request.ContentType;
            LitServer.FormData form = null;
            JsonRPCrequest req = null;
            if (ctype == "application/x-www-form-urlencoded" ||
                 (ctype.IndexOf("multipart/form-data;") == 0))
            {
                form = await LitServer.FormData.FromRequest(HttpContext.Request);
                var _jsonrpc = form.mapParams["jsonrpc"];
                var _id = long.Parse(form.mapParams["id"]);
                var _method = form.mapParams["method"];
                var _strparams = form.mapParams["params"];
                var _params = JArray.Parse(_strparams);
                req = new JsonRPCrequest
                {
                    jsonrpc = _jsonrpc,
                    method = _method,
                    @params = JsonConvert.DeserializeObject<object[]>(JsonConvert.SerializeObject(_params)),
                    id = _id
                };
            }
            else// if (ctype == "application/json") 其他所有请求方式都这样取好了
            {
                var text = await LitServer.FormData.GetStringFromRequest(HttpContext.Request);
                req = JsonConvert.DeserializeObject<JsonRPCrequest>(text);
            }
            return getRes(req);
        }

    }
}