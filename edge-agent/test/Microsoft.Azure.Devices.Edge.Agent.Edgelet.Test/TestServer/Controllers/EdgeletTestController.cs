// Copyright (c) Microsoft. All rights reserved.
//----------------------
// <auto-generated>
//     Generated using the NSwag toolchain v11.17.12.0 (NJsonSchema v9.10.50.0 (Newtonsoft.Json v9.0.0.0)) (http://NSwag.org)
// </auto-generated>
//----------------------

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Test.TestServer.Controllers
{
    #pragma warning disable // Disable all warnings

    [System.CodeDom.Compiler.GeneratedCode("NSwag", "11.17.12.0 (NJsonSchema v9.10.50.0 (Newtonsoft.Json v9.0.0.0))")]
    public interface IController
    {
        /// <summary>List modules.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <returns>Ok</returns>
        System.Threading.Tasks.Task<ModuleList> ListModulesAsync(string api_version);

        /// <summary>Create module.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="module"></param>
        /// <returns>Created</returns>
        System.Threading.Tasks.Task<ModuleDetails> CreateModuleAsync(string api_version, ModuleSpec module);

        /// <summary>Get a module's status.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the module to get. (urlencoded)</param>
        /// <returns>Ok</returns>
        System.Threading.Tasks.Task<ModuleDetails> GetModuleAsync(string api_version, string name);

        /// <summary>Update a module.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the module to update. (urlencoded)</param>
        /// <param name="start">Flag indicating whether module should be started after updating.</param>
        /// <param name="module"></param>
        /// <returns>Ok</returns>
        System.Threading.Tasks.Task<ModuleDetails> UpdateModuleAsync(string api_version, string name, bool start, ModuleSpec module);

        /// <summary>Prepare to update a module.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the module to update. (urlencoded)</param>
        /// <returns>No Content</returns>
        System.Threading.Tasks.Task PrepareUpdateModuleAsync(string api_version, string name, ModuleSpec module);

        /// <summary>Delete a module.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the module to delete. (urlencoded)</param>
        /// <returns>No Content</returns>
        System.Threading.Tasks.Task DeleteModuleAsync(string api_version, string name);

        /// <summary>Start a module.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the module to start. (urlencoded)</param>
        /// <returns>No Content</returns>
        System.Threading.Tasks.Task StartModuleAsync(string api_version, string name);

        /// <summary>Stop a module.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the module to stop. (urlencoded)</param>
        /// <returns>Status code</returns>
        System.Threading.Tasks.Task<int> StopModuleAsync(string api_version, string name);

        /// <summary>Restart a module.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the module to restart. (urlencoded)</param>
        /// <returns>No Content</returns>
        System.Threading.Tasks.Task RestartModuleAsync(string api_version, string name);

        /// <summary>List identities.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <returns>Ok</returns>
        System.Threading.Tasks.Task<IdentityList> ListIdentitiesAsync(string api_version);

        /// <summary>Create an identity.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="identity"></param>
        /// <returns>Created</returns>
        System.Threading.Tasks.Task<Identity> CreateIdentityAsync(string api_version, IdentitySpec identity);

        /// <summary>Update an identity.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the identity to update. (urlencoded)</param>
        /// <param name="updateinfo"></param>
        /// <returns>Updated</returns>
        System.Threading.Tasks.Task<Identity> UpdateIdentityAsync(string api_version, string name, UpdateIdentity updateinfo);

        /// <summary>Delete an identity.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the identity to delete. (urlencoded)</param>
        /// <returns>Ok</returns>
        System.Threading.Tasks.Task DeleteIdentityAsync(string api_version, string name);

    }

    [System.CodeDom.Compiler.GeneratedCode("NSwag", "11.17.12.0 (NJsonSchema v9.10.50.0 (Newtonsoft.Json v9.0.0.0))")]
    public partial class Controller : Microsoft.AspNetCore.Mvc.Controller
    {
        private IController _implementation;

        public Controller(IController implementation)
        {
            _implementation = implementation;
        }

        /// <summary>List modules.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <returns>Ok</returns>
        [Microsoft.AspNetCore.Mvc.HttpGet, Microsoft.AspNetCore.Mvc.Route("modules")]
        public System.Threading.Tasks.Task<ModuleList> ListModules(string api_version)
        {
            return _implementation.ListModulesAsync(api_version);
        }

        /// <summary>Create module.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="module"></param>
        /// <returns>Created</returns>
        [Microsoft.AspNetCore.Mvc.HttpPost, Microsoft.AspNetCore.Mvc.Route("modules")]
        public System.Threading.Tasks.Task<ModuleDetails> CreateModule(string api_version, [Microsoft.AspNetCore.Mvc.FromBody] ModuleSpec module)
        {
            return _implementation.CreateModuleAsync(api_version, module);
        }

        /// <summary>Get a module's status.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the module to get. (urlencoded)</param>
        /// <returns>Ok</returns>
        [Microsoft.AspNetCore.Mvc.HttpGet, Microsoft.AspNetCore.Mvc.Route("modules/{name}")]
        public System.Threading.Tasks.Task<ModuleDetails> GetModule(string api_version, string name)
        {
            return _implementation.GetModuleAsync(api_version, name);
        }

        /// <summary>Update a module.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the module to update. (urlencoded)</param>
        /// <param name="start">Flag indicating whether module should be started after updating.</param>
        /// <param name="module"></param>
        /// <returns>Ok</returns>
        [Microsoft.AspNetCore.Mvc.HttpPut, Microsoft.AspNetCore.Mvc.Route("modules/{name}")]
        public System.Threading.Tasks.Task<ModuleDetails> UpdateModule(string api_version, string name, bool? start, [Microsoft.AspNetCore.Mvc.FromBody] ModuleSpec module)
        {
            return _implementation.UpdateModuleAsync(api_version, name, start ?? false, module);
        }

        /// <summary>Prepare to update a module.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the module to update. (urlencoded)</param>
        /// <returns>No Content</returns>
        [Microsoft.AspNetCore.Mvc.HttpPost, Microsoft.AspNetCore.Mvc.Route("modules/{name}/prepareupdate")]
        public System.Threading.Tasks.Task PrepareUpdateModule(string api_version, string name, [Microsoft.AspNetCore.Mvc.FromBody] ModuleSpec module)
        {
            return _implementation.PrepareUpdateModuleAsync(api_version, name, module);
        }

        /// <summary>Delete a module.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the module to delete. (urlencoded)</param>
        /// <returns>No Content</returns>
        [Microsoft.AspNetCore.Mvc.HttpDelete, Microsoft.AspNetCore.Mvc.Route("modules/{name}")]
        public System.Threading.Tasks.Task DeleteModule(string api_version, string name)
        {
            return _implementation.DeleteModuleAsync(api_version, name);
        }

        /// <summary>Start a module.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the module to start. (urlencoded)</param>
        /// <returns>No Content</returns>
        [Microsoft.AspNetCore.Mvc.HttpPost, Microsoft.AspNetCore.Mvc.Route("modules/{name}/start")]
        public System.Threading.Tasks.Task StartModule(string api_version, string name)
        {
            return _implementation.StartModuleAsync(api_version, name);
        }

        /// <summary>Stop a module.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the module to stop. (urlencoded)</param>
        /// <returns>No Content</returns>
        [Microsoft.AspNetCore.Mvc.HttpPost, Microsoft.AspNetCore.Mvc.Route("modules/{name}/stop")]
        public async System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult> StopModule(string api_version, string name)
        {
            int statusCode = await _implementation.StopModuleAsync(api_version, name);
            return StatusCode(statusCode);
        }

        /// <summary>Restart a module.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the module to restart. (urlencoded)</param>
        /// <returns>No Content</returns>
        [Microsoft.AspNetCore.Mvc.HttpPost, Microsoft.AspNetCore.Mvc.Route("modules/{name}/restart")]
        public System.Threading.Tasks.Task RestartModule(string api_version, string name)
        {
            return _implementation.RestartModuleAsync(api_version, name);
        }

        /// <summary>List identities.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <returns>Ok</returns>
        [Microsoft.AspNetCore.Mvc.HttpGet, Microsoft.AspNetCore.Mvc.Route("identities/")]
        public System.Threading.Tasks.Task<IdentityList> ListIdentities(string api_version)
        {
            return _implementation.ListIdentitiesAsync(api_version);
        }

        /// <summary>Create an identity.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="identity"></param>
        /// <returns>Created</returns>
        [Microsoft.AspNetCore.Mvc.HttpPost, Microsoft.AspNetCore.Mvc.Route("identities/")]
        public System.Threading.Tasks.Task<Identity> CreateIdentity(string api_version, [Microsoft.AspNetCore.Mvc.FromBody] IdentitySpec identity)
        {
            return _implementation.CreateIdentityAsync(api_version, identity);
        }

        /// <summary>Update an identity.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the identity to update. (urlencoded)</param>
        /// <param name="updateinfo"></param>
        /// <returns>Updated</returns>
        [Microsoft.AspNetCore.Mvc.HttpPut, Microsoft.AspNetCore.Mvc.Route("identities/{name}")]
        public System.Threading.Tasks.Task<Identity> UpdateIdentity(string api_version, string name, [Microsoft.AspNetCore.Mvc.FromBody] UpdateIdentity updateinfo)
        {
            return _implementation.UpdateIdentityAsync(api_version, name, updateinfo);
        }

        /// <summary>Delete an identity.</summary>
        /// <param name="api_version">The version of the API.</param>
        /// <param name="name">The name of the identity to delete. (urlencoded)</param>
        /// <returns>Ok</returns>
        [Microsoft.AspNetCore.Mvc.HttpDelete, Microsoft.AspNetCore.Mvc.Route("identities/{name}")]
        public System.Threading.Tasks.Task DeleteIdentity(string api_version, string name)
        {
            return _implementation.DeleteIdentityAsync(api_version, name);
        }

    }



    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "9.10.50.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ModuleList : System.ComponentModel.INotifyPropertyChanged
    {
        private System.Collections.Generic.List<ModuleDetails> _modules = new System.Collections.Generic.List<ModuleDetails>();

        [Newtonsoft.Json.JsonProperty("modules", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public System.Collections.Generic.List<ModuleDetails> Modules
        {
            get { return _modules; }
            set
            {
                if (_modules != value)
                {
                    _modules = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static ModuleList FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<ModuleList>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "9.10.50.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ModuleDetails : System.ComponentModel.INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private string _type;
        private Config _config = new Config();
        private Status _status = new Status();

        /// <summary>System generated unique identitier.</summary>
        [Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string Id
        {
            get { return _id; }
            set
            {
                if (_id != value)
                {
                    _id = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>The name of the module.</summary>
        [Newtonsoft.Json.JsonProperty("name", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>The type of a module.</summary>
        [Newtonsoft.Json.JsonProperty("type", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string Type
        {
            get { return _type; }
            set
            {
                if (_type != value)
                {
                    _type = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("config", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public Config Config
        {
            get { return _config; }
            set
            {
                if (_config != value)
                {
                    _config = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("status", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public Status Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    _status = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static ModuleDetails FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<ModuleDetails>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "9.10.50.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ModuleSpec : System.ComponentModel.INotifyPropertyChanged
    {
        private string _name;
        private string _type;
        private Config _config = new Config();

        /// <summary>The name of a the module.</summary>
        [Newtonsoft.Json.JsonProperty("name", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("type", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string Type
        {
            get { return _type; }
            set
            {
                if (_type != value)
                {
                    _type = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("config", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public Config Config
        {
            get { return _config; }
            set
            {
                if (_config != value)
                {
                    _config = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static ModuleSpec FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<ModuleSpec>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "9.10.50.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class Config : System.ComponentModel.INotifyPropertyChanged
    {
        private object _settings = new object();
        private System.Collections.Generic.List<EnvVar> _env;

        [Newtonsoft.Json.JsonProperty("settings", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public object Settings
        {
            get { return _settings; }
            set
            {
                if (_settings != value)
                {
                    _settings = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("env", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public System.Collections.Generic.List<EnvVar> Env
        {
            get { return _env; }
            set
            {
                if (_env != value)
                {
                    _env = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static Config FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "9.10.50.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class Status : System.ComponentModel.INotifyPropertyChanged
    {
        private System.DateTime? _startTime;
        private ExitStatus _exitStatus;
        private RuntimeStatus _runtimeStatus = new RuntimeStatus();

        [Newtonsoft.Json.JsonProperty("startTime", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public System.DateTime? StartTime
        {
            get { return _startTime; }
            set
            {
                if (_startTime != value)
                {
                    _startTime = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("exitStatus", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public ExitStatus ExitStatus
        {
            get { return _exitStatus; }
            set
            {
                if (_exitStatus != value)
                {
                    _exitStatus = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("runtimeStatus", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public RuntimeStatus RuntimeStatus
        {
            get { return _runtimeStatus; }
            set
            {
                if (_runtimeStatus != value)
                {
                    _runtimeStatus = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static Status FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Status>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "9.10.50.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class EnvVar : System.ComponentModel.INotifyPropertyChanged
    {
        private string _key;
        private string _value;

        [Newtonsoft.Json.JsonProperty("key", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string Key
        {
            get { return _key; }
            set
            {
                if (_key != value)
                {
                    _key = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("value", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string Value
        {
            get { return _value; }
            set
            {
                if (_value != value)
                {
                    _value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static EnvVar FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<EnvVar>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "9.10.50.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ExitStatus : System.ComponentModel.INotifyPropertyChanged
    {
        private System.DateTime _exitTime;
        private string _statusCode;

        [Newtonsoft.Json.JsonProperty("exitTime", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public System.DateTime ExitTime
        {
            get { return _exitTime; }
            set
            {
                if (_exitTime != value)
                {
                    _exitTime = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("statusCode", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string StatusCode
        {
            get { return _statusCode; }
            set
            {
                if (_statusCode != value)
                {
                    _statusCode = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static ExitStatus FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<ExitStatus>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "9.10.50.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class RuntimeStatus : System.ComponentModel.INotifyPropertyChanged
    {
        private string _status;
        private string _description;

        [Newtonsoft.Json.JsonProperty("status", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    _status = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("description", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string Description
        {
            get { return _description; }
            set
            {
                if (_description != value)
                {
                    _description = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static RuntimeStatus FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<RuntimeStatus>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "9.10.50.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class IdentityList : System.ComponentModel.INotifyPropertyChanged
    {
        private System.Collections.Generic.List<Identity> _identities = new System.Collections.Generic.List<Identity>();

        [Newtonsoft.Json.JsonProperty("identities", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public System.Collections.Generic.List<Identity> Identities
        {
            get { return _identities; }
            set
            {
                if (_identities != value)
                {
                    _identities = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static IdentityList FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<IdentityList>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "9.10.50.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class IdentitySpec : System.ComponentModel.INotifyPropertyChanged
    {
        private string _moduleId;
        private string _managedBy;

        [Newtonsoft.Json.JsonProperty("moduleId", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string ModuleId
        {
            get { return _moduleId; }
            set
            {
                if (_moduleId != value)
                {
                    _moduleId = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("managedBy", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string ManagedBy
        {
            get { return _managedBy; }
            set
            {
                if (_managedBy != value)
                {
                    _managedBy = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static IdentitySpec FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<IdentitySpec>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "9.10.50.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class UpdateIdentity : System.ComponentModel.INotifyPropertyChanged
    {
        private string _generationId;
        private string _managedBy;

        [Newtonsoft.Json.JsonProperty("generationId", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string GenerationId
        {
            get { return _generationId; }
            set
            {
                if (_generationId != value)
                {
                    _generationId = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("managedBy", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string ManagedBy
        {
            get { return _managedBy; }
            set
            {
                if (_managedBy != value)
                {
                    _managedBy = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static UpdateIdentity FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<UpdateIdentity>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "9.10.50.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class Identity : System.ComponentModel.INotifyPropertyChanged
    {
        private string _moduleId;
        private string _managedBy;
        private string _generationId;
        private IdentityAuthType _authType;

        [Newtonsoft.Json.JsonProperty("moduleId", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string ModuleId
        {
            get { return _moduleId; }
            set
            {
                if (_moduleId != value)
                {
                    _moduleId = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("managedBy", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string ManagedBy
        {
            get { return _managedBy; }
            set
            {
                if (_managedBy != value)
                {
                    _managedBy = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("generationId", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string GenerationId
        {
            get { return _generationId; }
            set
            {
                if (_generationId != value)
                {
                    _generationId = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("authType", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public IdentityAuthType AuthType
        {
            get { return _authType; }
            set
            {
                if (_authType != value)
                {
                    _authType = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static Identity FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Identity>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "9.10.50.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ErrorResponse : System.ComponentModel.INotifyPropertyChanged
    {
        private string _message;

        [Newtonsoft.Json.JsonProperty("message", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public string Message
        {
            get { return _message; }
            set
            {
                if (_message != value)
                {
                    _message = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static ErrorResponse FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<ErrorResponse>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "9.10.50.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum IdentityAuthType
    {
        [System.Runtime.Serialization.EnumMember(Value = "None")]
        None = 0,

        [System.Runtime.Serialization.EnumMember(Value = "Sas")]
        Sas = 1,

        [System.Runtime.Serialization.EnumMember(Value = "X509")]
        X509 = 2,

    }

}
