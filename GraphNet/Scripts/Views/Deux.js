var newPropertVM = function (id) {
    let self = this;
    self.id = id;

    self.pOptions = [
        "age",
        "year born",
        "year died",
        "nameMeaning",
        "alternateName",
        "alternateSpelling",
        "other"
    ]

    self.pKey = ko.observable();
    self.pKey.subscribe(function (v) {
        
    });
    self.pKeyCustom = ko.observable();
    self.pValue = ko.observable();

    self.pSave = function () {
        let data = {
            key: self.pKey(),
            value: self.pValue()
        };
        if (self.pKey() == 'other')
            data.key = self.pKeyCustom();
        v.post("/NewProperty/" + self.id, data, function () {
            v.refreshMe();
        });
    }
}

var newChildVM = function (id) {
    let self = this;
    self.id = id;

    self.ncName = ko.observable();    
    
    self.ncMotherList = ko.observableArray();
    self.ncFatherList = ko.observableArray();

    self.ncMother = ko.observable();
    self.ncFather = ko.observable();

    self.ncFatherAge = ko.observable();
    self.ncMotherAge = ko.observable();

    self.ncGender = ko.observable();
    self.ncSave = function () {
        v.loading(true);
        v.get("/nameCheck/" + self.ncName(), function (d) {
            if (d.length == 0)
                self.saveNew();
            else {
                v.loading(false);
                var str = "<div>There are people in the network already that match that name -- " + self.ncName() ;
                            
                for (var i in d) {
                    str +=
                        '<div class="input-group-prepend">' +
                        '<div class="input-group-text">' +
                        '<input type="radio" name="chk" type="checkbox" value="' + d[i].me + '"/>' +
                        '</div>' +
                        '<div style="padding-left:5px;padding-right:5px;border:solid 1px #ced4da;border-top-right-radius:2px;border-bottom-right-radius:2px;width:100%;">' +
                        'Parents: ' + d[i].parents + ' ; Children: ' + d[i].children +
                        '</div>' +
                        '</div>';
                }

                str +=
                    '<div class="input-group-prepend">' +
                    '<div class="input-group-text">' +
                    '<input type="radio" name="chk" type="checkbox" value="new"/>' +
                    '</div>' +
                    '<div style="padding-left:5px;padding-right:5px;border:solid 1px #ced4da;border-top-right-radius:2px;border-bottom-right-radius:2px;width:100%;">' +
                    'Save as new person' +
                    '</div>' +
                    '</div>';

                str += "</div>";
                
                m.show(str, "New Child", "Select", function () {
                    let selected = $('input[name=chk]:checked').val(); 
                    if (selected == "new")
                        self.saveNew();
                    else
                        self.saveId(selected);
                });
            }                
        });
    }

    self.saveId = function (id) {
        var data = {
            name: self.ncName(),
            gender: self.ncGender(),
            motherAge: self.ncMotherAge(),
            fatherAge: self.ncFatherAge(),
            id: id
        }

        if (self.ncMother() != undefined)
            data.motherId = self.ncMother().id;
        if (self.ncFather() != undefined)
            data.fatherId = self.ncFather().id;

        v.post("/AssignChild/" + v.id, data, function () {
            v.refreshMe();
        });
    }
    self.saveNew = function () {
        var data = {
            name: self.ncName(),
            gender: self.ncGender(),         
            motherAge: self.ncMotherAge(),
            fatherAge: self.ncFatherAge(),
        }

        if (self.ncMother() != undefined)
            data.motherId = self.ncMother().id;
        if (self.ncFather() != undefined)
            data.fatherId = self.ncFather().id;

        v.post("/NewChild/" + v.id, data, function () {
            v.refreshMe();
        });
    }
}

var newAreaLivedVM = function (id) {
    let self = this;

    self.id = id;

    self.alArea = ko.observable();
    self.alCountry = ko.observable();
    self.alSave = function () {
        let data = {
            area: self.alArea(),
            country: self.alCountry()
        }
        v.post("/NewArea/" + v.id, data, function () {
            v.refreshMe();
        });
    }
}

var newSpouseVM = function (id) {
    let self = this;

    self.id = id;

    self.spName = ko.observable();
    self.spGender = ko.observable();
    self.spSave = function () {

    }
}

var modalVM = function () {
    let self = this;
    self.cb;

    $("#modalAction").click(function () {
        self.cb();
    });

    self.show = function (content, title, actionButtonCaption, actionButtonCallback) {
        $(".modal-title").html(title);
        $(".modal-body").html(content);

        $("#modal").modal('show');
        self.cb = actionButtonCallback;       
    }
}

var deuxVM = function () {
    let self = this;
    // selected person
    self.id = '';

    $("#searchButton").click(function () {
        $("#searchRow").show();
    });

    self.selectSearchResult = function (v) {
        var p = self.searchResults().filter(function (x) {
            return x.id == v.id;
        });
        if (p.length == 1) {
            self.person(p[0]);            
        }
    }
    self.graphJSON = ko.observable();

    self.person = ko.observable();  
    self.person.subscribe(function (v) {
        if (v != undefined) {
            self.id = v.id;
            v.newChild = new newChildVM(v.id);
            v.newSpouse = new newSpouseVM(v.id);
            v.newProp = new newPropertVM(v.id);
            v.newArea = new newAreaLivedVM(v.id);

            var data = {                
                nodes: [
                    {
                        vType: "rootPerson",
                        id: v.id,
                        caption: v.name,                        
                    }
                ],
                edges: [
                ]
            }
           
            for (var i in v.spouse) {
                data.nodes.push({ id: v.spouse[i].id, caption: v.spouse[i].name, vType: "person" });
                data.edges.push({ source: v.id, target: v.spouse[i].id, eType: "spouse", caption: "spouse" });
                v.spouse[i].url = "/home/deux#id=" + v.spouse[i].id;
            }
            for (var i in v.children) {
                data.nodes.push({ id: v.children[i].id, caption: v.children[i].name, vType: "person" });    
                data.edges.push({ source: v.id, target: v.children[i].id, eType: "parent", caption: "parent" });
            }
            if (v.gender != null) {
                if (v.gender == 'F') {                    
                    v.newSpouse.spGender('M');

                    v.newChild.ncMotherList([{ id: v.id, name: v.name }]);
                    v.newChild.ncMother({ id: v.id, name: v.name });
                    v.newChild.ncFatherList(v.spouse.slice());

                    if (v.spouse.length == 1)
                        v.newChild.ncFather(v.spouse[0].name);
                }
                else if (v.gender == 'M') {
                    v.newSpouse.spGender('F');

                    v.newChild.ncFatherList([{ id: v.id, name: v.name }]);
                    v.newChild.ncFather({ id: v.id, name: v.name });
                    v.newChild.ncMotherList(v.spouse.slice());

                    if (v.spouse.length == 1) 
                        v.newChild.ncMother(v.spouse[0].name);                    
                }                         
            }

            var conf = {
                nodeTypes: {
                    vType: ["person", "rootPerson"] },
                edgeTypes: {
                    eType: ["parent", "married"]
                },
                 
                graphHeight: function () { return 400; },
                graphWidth: function () { return 600; },

                nodeCaptionsOnByDefault: true,
                edgeCaptionsOnByDefault: true,

                edgeCaption: 'caption',
                nodeCaption: 'caption',                
                
                nodeStyle: {
                    rootPerson: {
                        color: "green",
                    },
                    person: {
                        color: "purple",
                    },
                },
                edgeStyle: {
                    parent: {
                        color: "white",
                    },
                    spouse: {
                        color: "yellow",
                    }
                },
                dataSource: data
            };
            
            alchemy.begin(conf);
            window.location = "/home/deux#id=" + v.id;
        } else
            self.newChild.reset();
    });

    self.searchName = ko.observable();
    self.searchParent = ko.observable();
    self.searchResults = ko.observableArray();

    self.idByName = function () {
        self.loading(true);
        let url = "/idByName/" + self.searchName() + "/";
        self.get(url, function (data) {
            self.processSearchResults(data.results);
        })
    }

    self.idByNameAndParent = function () {
        self.loading(true);
        self.searchResults.removeAll();
        if (self.searchParent() != undefined) {
            let url = "/idByNameAndParent/" + self.searchName() + "/" + self.searchParent() + "/";
            self.get(url, function (data) {
                self.processSearchResults(data.results);
            })
        } else {
            alert("??");
        }
    }

    self.processSearchResults = function (results) {
        for (var i in results) {
            var p = self.parsePerson(results[i]);
            p.info = i + 1;
            self.searchResults.push(p);
        }

        if (self.searchResults().length == 1)
            self.selectSearchResult(self.searchResults()[0]);

        self.loading(false);
    }

    self.parsePerson = function (person) {
        person.info = "";
        person.url = "/home/deux#id=" + person.id;

        for (var i in person.spouse) {     
            person.spouse[i].url = "/home/deux#id=" + person.spouse[i].id;
        }

        for (var i in person.parents) {
            person.parents[i].url = "/home/deux#id=" + person.parents[i].id;
        }

        for (var i in person.children) {
            person.children[i].url = "/home/deux#id=" + person.children[i].id;
        }

        for (var i in person.livedIn) {
            person.livedIn[i].url = "/home/deux#area=" + person.livedIn[i].key;
        }

        for (var i in person.siblings) {
            person.siblings[i].url = "/home/deux#id=" + person.siblings[i].id;
        }
        return person;
    }

    self.refreshMe = function () {
        self.loading(true);
        let url = "/personById/" + self.id + "/";
        self.searchResults.removeAll();
        self.get(url, function (data) {
            self.person(self.parsePerson(data.result));
            self.loading(false);
        });
    }

    self.getById = function (id) {
        if (id != self.id) {
            self.loading(true);
            let url = "/personById/" + id + "/";
            self.searchResults.removeAll();
            self.get(url, function (data) {
                self.person(self.parsePerson(data.result));
                self.loading(false);
            });
        }
    }

    self.getAreaById = function (id) {
        
    }

    /* Busy animation */;
    self.loading = function (show) {
        var loading = document.getElementById("loadingData");
        if (show)
            loading.style.display = "initial";
        else
            loading.style.display = "none";
    }

    self.post = function (path, json, callback) {
        var xmlhttp = new XMLHttpRequest();   // new HttpRequest instance
        xmlhttp.open("POST", path);
        if (typeof json != 'string') {
            json = JSON.stringify(json);
        }
        xmlhttp.responseType = 'json';
        xmlhttp.setRequestHeader("Content-Type", "application/json;charset=UTF-8");
        xmlhttp.addEventListener("load", function () {
            if (callback) {
                callback(this.response);
            }
        });
        xmlhttp.send(json);
    }
    self.get = function (path, callback) {
        var xmlhttp = new XMLHttpRequest();   // new HttpRequest instance
        xmlhttp.open("GET", path);
        xmlhttp.responseType = 'json';
        xmlhttp.setRequestHeader("Content-Type", "application/json;charset=UTF-8");
        xmlhttp.addEventListener("load", function () {
            if (callback) {
                callback(this.response);
            }
        });
        xmlhttp.send();
    }
}

var v = new deuxVM();
var m = new modalVM();

// when page loads, check if there's a Vertex to preload
function parseHash() {
    if (window.location.hash != undefined && window.location.hash != "") {
        var idx = window.location.hash.indexOf("=") + 1;        
        if (window.location.hash.substring(0, 3) == "#id") 
            v.getById(window.location.hash.substring(idx));        
        else if (window.location.hash.substring(0, 3) == "#area")
            v.getAreaById(window.location.hash.substring(idx));       
    }
}

// catch the forward/back browser navigation and process
window.onpopstate = function () {
    parseHash();
}

// start off page (wanted to try skipping JQuery for this site, as I've become too dependant on it)
document.addEventListener('DOMContentLoaded', function () {
    ko.applyBindings(v);
    v.loading(false);  
    if (window.location.hash != undefined && window.location.hash != "") {
        parseHash();
    } else {
        $('#searchRow').collapse('show');       
    }
});