// \0rolldown/runtime.js
var e = Object.create, t = Object.defineProperty, n = Object.getOwnPropertyDescriptor, r = Object.getOwnPropertyNames, i = Object.getPrototypeOf, a = Object.prototype.hasOwnProperty, o = (e, t) => () => (t || (e((t = { exports: {} }).exports, t, t.exports), e = null), t.exports), s = (e, i, o, s) => {
	if (i && typeof i == "object" || typeof i == "function") for (var c = r(i), l = 0, u = c.length, d; l < u; l++) d = c[l], !a.call(e, d) && d !== o && t(e, d, {
		get: ((e) => i[e]).bind(null, d),
		enumerable: !(s = n(i, d)) || s.enumerable
	});
	return e;
}, c = (n, r, o) => (o = n == null ? {} : e(i(n)), s(r || !n || !n.__esModule || !a.call(n, "default") ? t(o, "default", {
	value: n,
	enumerable: !0
}) : o, n)), l = /* @__PURE__ */ o(((e) => {
	var t = Symbol.for("react.transitional.element"), n = Symbol.for("react.portal"), r = Symbol.for("react.fragment"), i = Symbol.for("react.strict_mode"), a = Symbol.for("react.profiler"), o = Symbol.for("react.consumer"), s = Symbol.for("react.context"), c = Symbol.for("react.forward_ref"), l = Symbol.for("react.suspense"), u = Symbol.for("react.memo"), d = Symbol.for("react.lazy"), f = Symbol.iterator;
	function p(e) {
		return typeof e != "object" || !e ? null : (e = f && e[f] || e["@@iterator"], typeof e == "function" ? e : null);
	}
	var m = {
		isMounted: function() {
			return !1;
		},
		enqueueForceUpdate: function() {},
		enqueueReplaceState: function() {},
		enqueueSetState: function() {}
	}, h = Object.assign, g = {};
	function _(e, t, n) {
		this.props = e, this.context = t, this.refs = g, this.updater = n || m;
	}
	_.prototype.isReactComponent = {}, _.prototype.setState = function(e, t) {
		if (typeof e != "object" && typeof e != "function" && e != null) throw Error("takes an object of state variables to update or a function which returns an object of state variables.");
		this.updater.enqueueSetState(this, e, t, "setState");
	}, _.prototype.forceUpdate = function(e) {
		this.updater.enqueueForceUpdate(this, e, "forceUpdate");
	};
	function v() {}
	v.prototype = _.prototype;
	function y(e, t, n) {
		this.props = e, this.context = t, this.refs = g, this.updater = n || m;
	}
	var b = y.prototype = new v();
	b.constructor = y, h(b, _.prototype), b.isPureReactComponent = !0;
	var x = Array.isArray, S = {
		H: null,
		A: null,
		T: null,
		S: null,
		V: null
	}, C = Object.prototype.hasOwnProperty;
	function w(e, n, r, i, a, o) {
		return r = o.ref, {
			$$typeof: t,
			type: e,
			key: n,
			ref: r === void 0 ? null : r,
			props: o
		};
	}
	function T(e, t) {
		return w(e.type, t, void 0, void 0, void 0, e.props);
	}
	function E(e) {
		return typeof e == "object" && !!e && e.$$typeof === t;
	}
	function D(e) {
		var t = {
			"=": "=0",
			":": "=2"
		};
		return "$" + e.replace(/[=:]/g, function(e) {
			return t[e];
		});
	}
	var O = /\/+/g;
	function k(e, t) {
		return typeof e == "object" && e && e.key != null ? D("" + e.key) : t.toString(36);
	}
	function A() {}
	function j(e) {
		switch (e.status) {
			case "fulfilled": return e.value;
			case "rejected": throw e.reason;
			default: switch (typeof e.status == "string" ? e.then(A, A) : (e.status = "pending", e.then(function(t) {
				e.status === "pending" && (e.status = "fulfilled", e.value = t);
			}, function(t) {
				e.status === "pending" && (e.status = "rejected", e.reason = t);
			})), e.status) {
				case "fulfilled": return e.value;
				case "rejected": throw e.reason;
			}
		}
		throw e;
	}
	function M(e, r, i, a, o) {
		var s = typeof e;
		(s === "undefined" || s === "boolean") && (e = null);
		var c = !1;
		if (e === null) c = !0;
		else switch (s) {
			case "bigint":
			case "string":
			case "number":
				c = !0;
				break;
			case "object": switch (e.$$typeof) {
				case t:
				case n:
					c = !0;
					break;
				case d: return c = e._init, M(c(e._payload), r, i, a, o);
			}
		}
		if (c) return o = o(e), c = a === "" ? "." + k(e, 0) : a, x(o) ? (i = "", c != null && (i = c.replace(O, "$&/") + "/"), M(o, r, i, "", function(e) {
			return e;
		})) : o != null && (E(o) && (o = T(o, i + (o.key == null || e && e.key === o.key ? "" : ("" + o.key).replace(O, "$&/") + "/") + c)), r.push(o)), 1;
		c = 0;
		var l = a === "" ? "." : a + ":";
		if (x(e)) for (var u = 0; u < e.length; u++) a = e[u], s = l + k(a, u), c += M(a, r, i, s, o);
		else if (u = p(e), typeof u == "function") for (e = u.call(e), u = 0; !(a = e.next()).done;) a = a.value, s = l + k(a, u++), c += M(a, r, i, s, o);
		else if (s === "object") {
			if (typeof e.then == "function") return M(j(e), r, i, a, o);
			throw r = String(e), Error("Objects are not valid as a React child (found: " + (r === "[object Object]" ? "object with keys {" + Object.keys(e).join(", ") + "}" : r) + "). If you meant to render a collection of children, use an array instead.");
		}
		return c;
	}
	function ee(e, t, n) {
		if (e == null) return e;
		var r = [], i = 0;
		return M(e, r, "", "", function(e) {
			return t.call(n, e, i++);
		}), r;
	}
	function N(e) {
		if (e._status === -1) {
			var t = e._result;
			t = t(), t.then(function(t) {
				(e._status === 0 || e._status === -1) && (e._status = 1, e._result = t);
			}, function(t) {
				(e._status === 0 || e._status === -1) && (e._status = 2, e._result = t);
			}), e._status === -1 && (e._status = 0, e._result = t);
		}
		if (e._status === 1) return e._result.default;
		throw e._result;
	}
	var te = typeof reportError == "function" ? reportError : function(e) {
		if (typeof window == "object" && typeof window.ErrorEvent == "function") {
			var t = new window.ErrorEvent("error", {
				bubbles: !0,
				cancelable: !0,
				message: typeof e == "object" && e && typeof e.message == "string" ? String(e.message) : String(e),
				error: e
			});
			if (!window.dispatchEvent(t)) return;
		} else if (typeof process == "object" && typeof process.emit == "function") {
			process.emit("uncaughtException", e);
			return;
		}
		console.error(e);
	};
	function P() {}
	e.Children = {
		map: ee,
		forEach: function(e, t, n) {
			ee(e, function() {
				t.apply(this, arguments);
			}, n);
		},
		count: function(e) {
			var t = 0;
			return ee(e, function() {
				t++;
			}), t;
		},
		toArray: function(e) {
			return ee(e, function(e) {
				return e;
			}) || [];
		},
		only: function(e) {
			if (!E(e)) throw Error("React.Children.only expected to receive a single React element child.");
			return e;
		}
	}, e.Component = _, e.Fragment = r, e.Profiler = a, e.PureComponent = y, e.StrictMode = i, e.Suspense = l, e.__CLIENT_INTERNALS_DO_NOT_USE_OR_WARN_USERS_THEY_CANNOT_UPGRADE = S, e.__COMPILER_RUNTIME = {
		__proto__: null,
		c: function(e) {
			return S.H.useMemoCache(e);
		}
	}, e.cache = function(e) {
		return function() {
			return e.apply(null, arguments);
		};
	}, e.cloneElement = function(e, t, n) {
		if (e == null) throw Error("The argument must be a React element, but you passed " + e + ".");
		var r = h({}, e.props), i = e.key, a = void 0;
		if (t != null) for (o in t.ref !== void 0 && (a = void 0), t.key !== void 0 && (i = "" + t.key), t) !C.call(t, o) || o === "key" || o === "__self" || o === "__source" || o === "ref" && t.ref === void 0 || (r[o] = t[o]);
		var o = arguments.length - 2;
		if (o === 1) r.children = n;
		else if (1 < o) {
			for (var s = Array(o), c = 0; c < o; c++) s[c] = arguments[c + 2];
			r.children = s;
		}
		return w(e.type, i, void 0, void 0, a, r);
	}, e.createContext = function(e) {
		return e = {
			$$typeof: s,
			_currentValue: e,
			_currentValue2: e,
			_threadCount: 0,
			Provider: null,
			Consumer: null
		}, e.Provider = e, e.Consumer = {
			$$typeof: o,
			_context: e
		}, e;
	}, e.createElement = function(e, t, n) {
		var r, i = {}, a = null;
		if (t != null) for (r in t.key !== void 0 && (a = "" + t.key), t) C.call(t, r) && r !== "key" && r !== "__self" && r !== "__source" && (i[r] = t[r]);
		var o = arguments.length - 2;
		if (o === 1) i.children = n;
		else if (1 < o) {
			for (var s = Array(o), c = 0; c < o; c++) s[c] = arguments[c + 2];
			i.children = s;
		}
		if (e && e.defaultProps) for (r in o = e.defaultProps, o) i[r] === void 0 && (i[r] = o[r]);
		return w(e, a, void 0, void 0, null, i);
	}, e.createRef = function() {
		return { current: null };
	}, e.forwardRef = function(e) {
		return {
			$$typeof: c,
			render: e
		};
	}, e.isValidElement = E, e.lazy = function(e) {
		return {
			$$typeof: d,
			_payload: {
				_status: -1,
				_result: e
			},
			_init: N
		};
	}, e.memo = function(e, t) {
		return {
			$$typeof: u,
			type: e,
			compare: t === void 0 ? null : t
		};
	}, e.startTransition = function(e) {
		var t = S.T, n = {};
		S.T = n;
		try {
			var r = e(), i = S.S;
			i !== null && i(n, r), typeof r == "object" && r && typeof r.then == "function" && r.then(P, te);
		} catch (e) {
			te(e);
		} finally {
			S.T = t;
		}
	}, e.unstable_useCacheRefresh = function() {
		return S.H.useCacheRefresh();
	}, e.use = function(e) {
		return S.H.use(e);
	}, e.useActionState = function(e, t, n) {
		return S.H.useActionState(e, t, n);
	}, e.useCallback = function(e, t) {
		return S.H.useCallback(e, t);
	}, e.useContext = function(e) {
		return S.H.useContext(e);
	}, e.useDebugValue = function() {}, e.useDeferredValue = function(e, t) {
		return S.H.useDeferredValue(e, t);
	}, e.useEffect = function(e, t, n) {
		var r = S.H;
		if (typeof n == "function") throw Error("useEffect CRUD overload is not enabled in this build of React.");
		return r.useEffect(e, t);
	}, e.useId = function() {
		return S.H.useId();
	}, e.useImperativeHandle = function(e, t, n) {
		return S.H.useImperativeHandle(e, t, n);
	}, e.useInsertionEffect = function(e, t) {
		return S.H.useInsertionEffect(e, t);
	}, e.useLayoutEffect = function(e, t) {
		return S.H.useLayoutEffect(e, t);
	}, e.useMemo = function(e, t) {
		return S.H.useMemo(e, t);
	}, e.useOptimistic = function(e, t) {
		return S.H.useOptimistic(e, t);
	}, e.useReducer = function(e, t, n) {
		return S.H.useReducer(e, t, n);
	}, e.useRef = function(e) {
		return S.H.useRef(e);
	}, e.useState = function(e) {
		return S.H.useState(e);
	}, e.useSyncExternalStore = function(e, t, n) {
		return S.H.useSyncExternalStore(e, t, n);
	}, e.useTransition = function() {
		return S.H.useTransition();
	}, e.version = "19.1.0";
})), u = /* @__PURE__ */ o(((e, t) => {
	t.exports = l();
})), d = /* @__PURE__ */ o(((e) => {
	function t(e, t) {
		var n = e.length;
		e.push(t);
		a: for (; 0 < n;) {
			var r = n - 1 >>> 1, a = e[r];
			if (0 < i(a, t)) e[r] = t, e[n] = a, n = r;
			else break a;
		}
	}
	function n(e) {
		return e.length === 0 ? null : e[0];
	}
	function r(e) {
		if (e.length === 0) return null;
		var t = e[0], n = e.pop();
		if (n !== t) {
			e[0] = n;
			a: for (var r = 0, a = e.length, o = a >>> 1; r < o;) {
				var s = 2 * (r + 1) - 1, c = e[s], l = s + 1, u = e[l];
				if (0 > i(c, n)) l < a && 0 > i(u, c) ? (e[r] = u, e[l] = n, r = l) : (e[r] = c, e[s] = n, r = s);
				else if (l < a && 0 > i(u, n)) e[r] = u, e[l] = n, r = l;
				else break a;
			}
		}
		return t;
	}
	function i(e, t) {
		var n = e.sortIndex - t.sortIndex;
		return n === 0 ? e.id - t.id : n;
	}
	if (e.unstable_now = void 0, typeof performance == "object" && typeof performance.now == "function") {
		var a = performance;
		e.unstable_now = function() {
			return a.now();
		};
	} else {
		var o = Date, s = o.now();
		e.unstable_now = function() {
			return o.now() - s;
		};
	}
	var c = [], l = [], u = 1, d = null, f = 3, p = !1, m = !1, h = !1, g = !1, _ = typeof setTimeout == "function" ? setTimeout : null, v = typeof clearTimeout == "function" ? clearTimeout : null, y = typeof setImmediate < "u" ? setImmediate : null;
	function b(e) {
		for (var i = n(l); i !== null;) {
			if (i.callback === null) r(l);
			else if (i.startTime <= e) r(l), i.sortIndex = i.expirationTime, t(c, i);
			else break;
			i = n(l);
		}
	}
	function x(e) {
		if (h = !1, b(e), !m) {
			if (n(c) !== null) m = !0, S || (S = !0, O());
			else {
				var t = n(l);
				t !== null && j(x, t.startTime - e);
			}
		}
	}
	var S = !1, C = -1, w = 5, T = -1;
	function E() {
		return g ? !0 : !(e.unstable_now() - T < w);
	}
	function D() {
		if (g = !1, S) {
			var t = e.unstable_now();
			T = t;
			var i = !0;
			try {
				a: {
					m = !1, h && (h = !1, v(C), C = -1), p = !0;
					var a = f;
					try {
						b: {
							for (b(t), d = n(c); d !== null && !(d.expirationTime > t && E());) {
								var o = d.callback;
								if (typeof o == "function") {
									d.callback = null, f = d.priorityLevel;
									var s = o(d.expirationTime <= t);
									if (t = e.unstable_now(), typeof s == "function") {
										d.callback = s, b(t), i = !0;
										break b;
									}
									d === n(c) && r(c), b(t);
								} else r(c);
								d = n(c);
							}
							if (d !== null) i = !0;
							else {
								var u = n(l);
								u !== null && j(x, u.startTime - t), i = !1;
							}
						}
						break a;
					} finally {
						d = null, f = a, p = !1;
					}
					i = void 0;
				}
			} finally {
				i ? O() : S = !1;
			}
		}
	}
	var O;
	if (typeof y == "function") O = function() {
		y(D);
	};
	else if (typeof MessageChannel < "u") {
		var k = new MessageChannel(), A = k.port2;
		k.port1.onmessage = D, O = function() {
			A.postMessage(null);
		};
	} else O = function() {
		_(D, 0);
	};
	function j(t, n) {
		C = _(function() {
			t(e.unstable_now());
		}, n);
	}
	e.unstable_IdlePriority = 5, e.unstable_ImmediatePriority = 1, e.unstable_LowPriority = 4, e.unstable_NormalPriority = 3, e.unstable_Profiling = null, e.unstable_UserBlockingPriority = 2, e.unstable_cancelCallback = function(e) {
		e.callback = null;
	}, e.unstable_forceFrameRate = function(e) {
		0 > e || 125 < e ? console.error("forceFrameRate takes a positive int between 0 and 125, forcing frame rates higher than 125 fps is not supported") : w = 0 < e ? Math.floor(1e3 / e) : 5;
	}, e.unstable_getCurrentPriorityLevel = function() {
		return f;
	}, e.unstable_next = function(e) {
		switch (f) {
			case 1:
			case 2:
			case 3:
				var t = 3;
				break;
			default: t = f;
		}
		var n = f;
		f = t;
		try {
			return e();
		} finally {
			f = n;
		}
	}, e.unstable_requestPaint = function() {
		g = !0;
	}, e.unstable_runWithPriority = function(e, t) {
		switch (e) {
			case 1:
			case 2:
			case 3:
			case 4:
			case 5: break;
			default: e = 3;
		}
		var n = f;
		f = e;
		try {
			return t();
		} finally {
			f = n;
		}
	}, e.unstable_scheduleCallback = function(r, i, a) {
		var o = e.unstable_now();
		switch (typeof a == "object" && a ? (a = a.delay, a = typeof a == "number" && 0 < a ? o + a : o) : a = o, r) {
			case 1:
				var s = -1;
				break;
			case 2:
				s = 250;
				break;
			case 5:
				s = 1073741823;
				break;
			case 4:
				s = 1e4;
				break;
			default: s = 5e3;
		}
		return s = a + s, r = {
			id: u++,
			callback: i,
			priorityLevel: r,
			startTime: a,
			expirationTime: s,
			sortIndex: -1
		}, a > o ? (r.sortIndex = a, t(l, r), n(c) === null && r === n(l) && (h ? (v(C), C = -1) : h = !0, j(x, a - o))) : (r.sortIndex = s, t(c, r), m || p || (m = !0, S || (S = !0, O()))), r;
	}, e.unstable_shouldYield = E, e.unstable_wrapCallback = function(e) {
		var t = f;
		return function() {
			var n = f;
			f = t;
			try {
				return e.apply(this, arguments);
			} finally {
				f = n;
			}
		};
	};
})), f = /* @__PURE__ */ o(((e, t) => {
	t.exports = d();
})), p = /* @__PURE__ */ o(((e) => {
	var t = u();
	function n(e) {
		var t = "https://react.dev/errors/" + e;
		if (1 < arguments.length) {
			t += "?args[]=" + encodeURIComponent(arguments[1]);
			for (var n = 2; n < arguments.length; n++) t += "&args[]=" + encodeURIComponent(arguments[n]);
		}
		return "Minified React error #" + e + "; visit " + t + " for the full message or use the non-minified dev environment for full errors and additional helpful warnings.";
	}
	function r() {}
	var i = {
		d: {
			f: r,
			r: function() {
				throw Error(n(522));
			},
			D: r,
			C: r,
			L: r,
			m: r,
			X: r,
			S: r,
			M: r
		},
		p: 0,
		findDOMNode: null
	}, a = Symbol.for("react.portal");
	function o(e, t, n) {
		var r = 3 < arguments.length && arguments[3] !== void 0 ? arguments[3] : null;
		return {
			$$typeof: a,
			key: r == null ? null : "" + r,
			children: e,
			containerInfo: t,
			implementation: n
		};
	}
	var s = t.__CLIENT_INTERNALS_DO_NOT_USE_OR_WARN_USERS_THEY_CANNOT_UPGRADE;
	function c(e, t) {
		if (e === "font") return "";
		if (typeof t == "string") return t === "use-credentials" ? t : "";
	}
	e.__DOM_INTERNALS_DO_NOT_USE_OR_WARN_USERS_THEY_CANNOT_UPGRADE = i, e.createPortal = function(e, t) {
		var r = 2 < arguments.length && arguments[2] !== void 0 ? arguments[2] : null;
		if (!t || t.nodeType !== 1 && t.nodeType !== 9 && t.nodeType !== 11) throw Error(n(299));
		return o(e, t, null, r);
	}, e.flushSync = function(e) {
		var t = s.T, n = i.p;
		try {
			if (s.T = null, i.p = 2, e) return e();
		} finally {
			s.T = t, i.p = n, i.d.f();
		}
	}, e.preconnect = function(e, t) {
		typeof e == "string" && (t ? (t = t.crossOrigin, t = typeof t == "string" ? t === "use-credentials" ? t : "" : void 0) : t = null, i.d.C(e, t));
	}, e.prefetchDNS = function(e) {
		typeof e == "string" && i.d.D(e);
	}, e.preinit = function(e, t) {
		if (typeof e == "string" && t && typeof t.as == "string") {
			var n = t.as, r = c(n, t.crossOrigin), a = typeof t.integrity == "string" ? t.integrity : void 0, o = typeof t.fetchPriority == "string" ? t.fetchPriority : void 0;
			n === "style" ? i.d.S(e, typeof t.precedence == "string" ? t.precedence : void 0, {
				crossOrigin: r,
				integrity: a,
				fetchPriority: o
			}) : n === "script" && i.d.X(e, {
				crossOrigin: r,
				integrity: a,
				fetchPriority: o,
				nonce: typeof t.nonce == "string" ? t.nonce : void 0
			});
		}
	}, e.preinitModule = function(e, t) {
		if (typeof e == "string") {
			if (typeof t == "object" && t) {
				if (t.as == null || t.as === "script") {
					var n = c(t.as, t.crossOrigin);
					i.d.M(e, {
						crossOrigin: n,
						integrity: typeof t.integrity == "string" ? t.integrity : void 0,
						nonce: typeof t.nonce == "string" ? t.nonce : void 0
					});
				}
			} else t ?? i.d.M(e);
		}
	}, e.preload = function(e, t) {
		if (typeof e == "string" && typeof t == "object" && t && typeof t.as == "string") {
			var n = t.as, r = c(n, t.crossOrigin);
			i.d.L(e, n, {
				crossOrigin: r,
				integrity: typeof t.integrity == "string" ? t.integrity : void 0,
				nonce: typeof t.nonce == "string" ? t.nonce : void 0,
				type: typeof t.type == "string" ? t.type : void 0,
				fetchPriority: typeof t.fetchPriority == "string" ? t.fetchPriority : void 0,
				referrerPolicy: typeof t.referrerPolicy == "string" ? t.referrerPolicy : void 0,
				imageSrcSet: typeof t.imageSrcSet == "string" ? t.imageSrcSet : void 0,
				imageSizes: typeof t.imageSizes == "string" ? t.imageSizes : void 0,
				media: typeof t.media == "string" ? t.media : void 0
			});
		}
	}, e.preloadModule = function(e, t) {
		if (typeof e == "string") {
			if (t) {
				var n = c(t.as, t.crossOrigin);
				i.d.m(e, {
					as: typeof t.as == "string" && t.as !== "script" ? t.as : void 0,
					crossOrigin: n,
					integrity: typeof t.integrity == "string" ? t.integrity : void 0
				});
			} else i.d.m(e);
		}
	}, e.requestFormReset = function(e) {
		i.d.r(e);
	}, e.unstable_batchedUpdates = function(e, t) {
		return e(t);
	}, e.useFormState = function(e, t, n) {
		return s.H.useFormState(e, t, n);
	}, e.useFormStatus = function() {
		return s.H.useHostTransitionStatus();
	}, e.version = "19.1.0";
})), m = /* @__PURE__ */ o(((e, t) => {
	function n() {
		if (typeof __REACT_DEVTOOLS_GLOBAL_HOOK__ < "u" && typeof __REACT_DEVTOOLS_GLOBAL_HOOK__.checkDCE == "function") try {
			__REACT_DEVTOOLS_GLOBAL_HOOK__.checkDCE(n);
		} catch (e) {
			console.error(e);
		}
	}
	n(), t.exports = p();
})), h = /* @__PURE__ */ o(((e) => {
	var t = f(), n = u(), r = m();
	function i(e) {
		var t = "https://react.dev/errors/" + e;
		if (1 < arguments.length) {
			t += "?args[]=" + encodeURIComponent(arguments[1]);
			for (var n = 2; n < arguments.length; n++) t += "&args[]=" + encodeURIComponent(arguments[n]);
		}
		return "Minified React error #" + e + "; visit " + t + " for the full message or use the non-minified dev environment for full errors and additional helpful warnings.";
	}
	function a(e) {
		return !(!e || e.nodeType !== 1 && e.nodeType !== 9 && e.nodeType !== 11);
	}
	function o(e) {
		var t = e, n = e;
		if (e.alternate) for (; t.return;) t = t.return;
		else {
			e = t;
			do
				t = e, t.flags & 4098 && (n = t.return), e = t.return;
			while (e);
		}
		return t.tag === 3 ? n : null;
	}
	function s(e) {
		if (e.tag === 13) {
			var t = e.memoizedState;
			if (t === null && (e = e.alternate, e !== null && (t = e.memoizedState)), t !== null) return t.dehydrated;
		}
		return null;
	}
	function c(e) {
		if (o(e) !== e) throw Error(i(188));
	}
	function l(e) {
		var t = e.alternate;
		if (!t) {
			if (t = o(e), t === null) throw Error(i(188));
			return t === e ? e : null;
		}
		for (var n = e, r = t;;) {
			var a = n.return;
			if (a === null) break;
			var s = a.alternate;
			if (s === null) {
				if (r = a.return, r !== null) {
					n = r;
					continue;
				}
				break;
			}
			if (a.child === s.child) {
				for (s = a.child; s;) {
					if (s === n) return c(a), e;
					if (s === r) return c(a), t;
					s = s.sibling;
				}
				throw Error(i(188));
			}
			if (n.return !== r.return) n = a, r = s;
			else {
				for (var l = !1, u = a.child; u;) {
					if (u === n) {
						l = !0, n = a, r = s;
						break;
					}
					if (u === r) {
						l = !0, r = a, n = s;
						break;
					}
					u = u.sibling;
				}
				if (!l) {
					for (u = s.child; u;) {
						if (u === n) {
							l = !0, n = s, r = a;
							break;
						}
						if (u === r) {
							l = !0, r = s, n = a;
							break;
						}
						u = u.sibling;
					}
					if (!l) throw Error(i(189));
				}
			}
			if (n.alternate !== r) throw Error(i(190));
		}
		if (n.tag !== 3) throw Error(i(188));
		return n.stateNode.current === n ? e : t;
	}
	function d(e) {
		var t = e.tag;
		if (t === 5 || t === 26 || t === 27 || t === 6) return e;
		for (e = e.child; e !== null;) {
			if (t = d(e), t !== null) return t;
			e = e.sibling;
		}
		return null;
	}
	var p = Object.assign, h = Symbol.for("react.element"), g = Symbol.for("react.transitional.element"), _ = Symbol.for("react.portal"), v = Symbol.for("react.fragment"), y = Symbol.for("react.strict_mode"), b = Symbol.for("react.profiler"), x = Symbol.for("react.provider"), S = Symbol.for("react.consumer"), C = Symbol.for("react.context"), w = Symbol.for("react.forward_ref"), T = Symbol.for("react.suspense"), E = Symbol.for("react.suspense_list"), D = Symbol.for("react.memo"), O = Symbol.for("react.lazy"), k = Symbol.for("react.activity"), A = Symbol.for("react.memo_cache_sentinel"), j = Symbol.iterator;
	function M(e) {
		return typeof e != "object" || !e ? null : (e = j && e[j] || e["@@iterator"], typeof e == "function" ? e : null);
	}
	var ee = Symbol.for("react.client.reference");
	function N(e) {
		if (e == null) return null;
		if (typeof e == "function") return e.$$typeof === ee ? null : e.displayName || e.name || null;
		if (typeof e == "string") return e;
		switch (e) {
			case v: return "Fragment";
			case b: return "Profiler";
			case y: return "StrictMode";
			case T: return "Suspense";
			case E: return "SuspenseList";
			case k: return "Activity";
		}
		if (typeof e == "object") switch (e.$$typeof) {
			case _: return "Portal";
			case C: return (e.displayName || "Context") + ".Provider";
			case S: return (e._context.displayName || "Context") + ".Consumer";
			case w:
				var t = e.render;
				return e = e.displayName, e ||= (e = t.displayName || t.name || "", e === "" ? "ForwardRef" : "ForwardRef(" + e + ")"), e;
			case D: return t = e.displayName || null, t === null ? N(e.type) || "Memo" : t;
			case O:
				t = e._payload, e = e._init;
				try {
					return N(e(t));
				} catch {}
		}
		return null;
	}
	var te = Array.isArray, P = n.__CLIENT_INTERNALS_DO_NOT_USE_OR_WARN_USERS_THEY_CANNOT_UPGRADE, F = r.__DOM_INTERNALS_DO_NOT_USE_OR_WARN_USERS_THEY_CANNOT_UPGRADE, ne = {
		pending: !1,
		data: null,
		method: null,
		action: null
	}, re = [], I = -1;
	function L(e) {
		return { current: e };
	}
	function R(e) {
		0 > I || (e.current = re[I], re[I] = null, I--);
	}
	function z(e, t) {
		I++, re[I] = e.current, e.current = t;
	}
	var ie = L(null), ae = L(null), oe = L(null), se = L(null);
	function ce(e, t) {
		switch (z(oe, t), z(ae, e), z(ie, null), t.nodeType) {
			case 9:
			case 11:
				e = (e = t.documentElement) && (e = e.namespaceURI) ? Dd(e) : 0;
				break;
			default: if (e = t.tagName, t = t.namespaceURI) t = Dd(t), e = Od(t, e);
			else switch (e) {
				case "svg":
					e = 1;
					break;
				case "math":
					e = 2;
					break;
				default: e = 0;
			}
		}
		R(ie), z(ie, e);
	}
	function le() {
		R(ie), R(ae), R(oe);
	}
	function ue(e) {
		e.memoizedState !== null && z(se, e);
		var t = ie.current, n = Od(t, e.type);
		t !== n && (z(ae, e), z(ie, n));
	}
	function de(e) {
		ae.current === e && (R(ie), R(ae)), se.current === e && (R(se), Ff._currentValue = ne);
	}
	var fe = Object.prototype.hasOwnProperty, pe = t.unstable_scheduleCallback, me = t.unstable_cancelCallback, he = t.unstable_shouldYield, ge = t.unstable_requestPaint, _e = t.unstable_now, ve = t.unstable_getCurrentPriorityLevel, ye = t.unstable_ImmediatePriority, be = t.unstable_UserBlockingPriority, xe = t.unstable_NormalPriority, Se = t.unstable_LowPriority, Ce = t.unstable_IdlePriority, we = t.log, Te = t.unstable_setDisableYieldValue, Ee = null, De = null;
	function Oe(e) {
		if (typeof we == "function" && Te(e), De && typeof De.setStrictMode == "function") try {
			De.setStrictMode(Ee, e);
		} catch {}
	}
	var ke = Math.clz32 ? Math.clz32 : Me, Ae = Math.log, je = Math.LN2;
	function Me(e) {
		return e >>>= 0, e === 0 ? 32 : 31 - (Ae(e) / je | 0) | 0;
	}
	var Ne = 256, Pe = 4194304;
	function Fe(e) {
		var t = e & 42;
		if (t !== 0) return t;
		switch (e & -e) {
			case 1: return 1;
			case 2: return 2;
			case 4: return 4;
			case 8: return 8;
			case 16: return 16;
			case 32: return 32;
			case 64: return 64;
			case 128: return 128;
			case 256:
			case 512:
			case 1024:
			case 2048:
			case 4096:
			case 8192:
			case 16384:
			case 32768:
			case 65536:
			case 131072:
			case 262144:
			case 524288:
			case 1048576:
			case 2097152: return e & 4194048;
			case 4194304:
			case 8388608:
			case 16777216:
			case 33554432: return e & 62914560;
			case 67108864: return 67108864;
			case 134217728: return 134217728;
			case 268435456: return 268435456;
			case 536870912: return 536870912;
			case 1073741824: return 0;
			default: return e;
		}
	}
	function Ie(e, t, n) {
		var r = e.pendingLanes;
		if (r === 0) return 0;
		var i = 0, a = e.suspendedLanes, o = e.pingedLanes;
		e = e.warmLanes;
		var s = r & 134217727;
		return s === 0 ? (s = r & ~a, s === 0 ? o === 0 ? n || (n = r & ~e, n !== 0 && (i = Fe(n))) : i = Fe(o) : i = Fe(s)) : (r = s & ~a, r === 0 ? (o &= s, o === 0 ? n || (n = s & ~e, n !== 0 && (i = Fe(n))) : i = Fe(o)) : i = Fe(r)), i === 0 ? 0 : t !== 0 && t !== i && (t & a) === 0 && (a = i & -i, n = t & -t, a >= n || a === 32 && n & 4194048) ? t : i;
	}
	function Le(e, t) {
		return (e.pendingLanes & ~(e.suspendedLanes & ~e.pingedLanes) & t) === 0;
	}
	function Re(e, t) {
		switch (e) {
			case 1:
			case 2:
			case 4:
			case 8:
			case 64: return t + 250;
			case 16:
			case 32:
			case 128:
			case 256:
			case 512:
			case 1024:
			case 2048:
			case 4096:
			case 8192:
			case 16384:
			case 32768:
			case 65536:
			case 131072:
			case 262144:
			case 524288:
			case 1048576:
			case 2097152: return t + 5e3;
			case 4194304:
			case 8388608:
			case 16777216:
			case 33554432: return -1;
			case 67108864:
			case 134217728:
			case 268435456:
			case 536870912:
			case 1073741824: return -1;
			default: return -1;
		}
	}
	function ze() {
		var e = Ne;
		return Ne <<= 1, !(Ne & 4194048) && (Ne = 256), e;
	}
	function Be() {
		var e = Pe;
		return Pe <<= 1, !(Pe & 62914560) && (Pe = 4194304), e;
	}
	function Ve(e) {
		for (var t = [], n = 0; 31 > n; n++) t.push(e);
		return t;
	}
	function He(e, t) {
		e.pendingLanes |= t, t !== 268435456 && (e.suspendedLanes = 0, e.pingedLanes = 0, e.warmLanes = 0);
	}
	function Ue(e, t, n, r, i, a) {
		var o = e.pendingLanes;
		e.pendingLanes = n, e.suspendedLanes = 0, e.pingedLanes = 0, e.warmLanes = 0, e.expiredLanes &= n, e.entangledLanes &= n, e.errorRecoveryDisabledLanes &= n, e.shellSuspendCounter = 0;
		var s = e.entanglements, c = e.expirationTimes, l = e.hiddenUpdates;
		for (n = o & ~n; 0 < n;) {
			var u = 31 - ke(n), d = 1 << u;
			s[u] = 0, c[u] = -1;
			var f = l[u];
			if (f !== null) for (l[u] = null, u = 0; u < f.length; u++) {
				var p = f[u];
				p !== null && (p.lane &= -536870913);
			}
			n &= ~d;
		}
		r !== 0 && We(e, r, 0), a !== 0 && i === 0 && e.tag !== 0 && (e.suspendedLanes |= a & ~(o & ~t));
	}
	function We(e, t, n) {
		e.pendingLanes |= t, e.suspendedLanes &= ~t;
		var r = 31 - ke(t);
		e.entangledLanes |= t, e.entanglements[r] = e.entanglements[r] | 1073741824 | n & 4194090;
	}
	function Ge(e, t) {
		var n = e.entangledLanes |= t;
		for (e = e.entanglements; n;) {
			var r = 31 - ke(n), i = 1 << r;
			i & t | e[r] & t && (e[r] |= t), n &= ~i;
		}
	}
	function Ke(e) {
		switch (e) {
			case 2:
				e = 1;
				break;
			case 8:
				e = 4;
				break;
			case 32:
				e = 16;
				break;
			case 256:
			case 512:
			case 1024:
			case 2048:
			case 4096:
			case 8192:
			case 16384:
			case 32768:
			case 65536:
			case 131072:
			case 262144:
			case 524288:
			case 1048576:
			case 2097152:
			case 4194304:
			case 8388608:
			case 16777216:
			case 33554432:
				e = 128;
				break;
			case 268435456:
				e = 134217728;
				break;
			default: e = 0;
		}
		return e;
	}
	function qe(e) {
		return e &= -e, 2 < e ? 8 < e ? e & 134217727 ? 32 : 268435456 : 8 : 2;
	}
	function Je() {
		var e = F.p;
		return e === 0 ? (e = window.event, e === void 0 ? 32 : Xf(e.type)) : e;
	}
	function Ye(e, t) {
		var n = F.p;
		try {
			return F.p = e, t();
		} finally {
			F.p = n;
		}
	}
	var Xe = Math.random().toString(36).slice(2), Ze = "__reactFiber$" + Xe, Qe = "__reactProps$" + Xe, $e = "__reactContainer$" + Xe, et = "__reactEvents$" + Xe, tt = "__reactListeners$" + Xe, nt = "__reactHandles$" + Xe, rt = "__reactResources$" + Xe, it = "__reactMarker$" + Xe;
	function at(e) {
		delete e[Ze], delete e[Qe], delete e[et], delete e[tt], delete e[nt];
	}
	function ot(e) {
		var t = e[Ze];
		if (t) return t;
		for (var n = e.parentNode; n;) {
			if (t = n[$e] || n[Ze]) {
				if (n = t.alternate, t.child !== null || n !== null && n.child !== null) for (e = Kd(e); e !== null;) {
					if (n = e[Ze]) return n;
					e = Kd(e);
				}
				return t;
			}
			e = n, n = e.parentNode;
		}
		return null;
	}
	function st(e) {
		if (e = e[Ze] || e[$e]) {
			var t = e.tag;
			if (t === 5 || t === 6 || t === 13 || t === 26 || t === 27 || t === 3) return e;
		}
		return null;
	}
	function ct(e) {
		var t = e.tag;
		if (t === 5 || t === 26 || t === 27 || t === 6) return e.stateNode;
		throw Error(i(33));
	}
	function lt(e) {
		var t = e[rt];
		return t ||= e[rt] = {
			hoistableStyles: /* @__PURE__ */ new Map(),
			hoistableScripts: /* @__PURE__ */ new Map()
		}, t;
	}
	function ut(e) {
		e[it] = !0;
	}
	var dt = /* @__PURE__ */ new Set(), ft = {};
	function pt(e, t) {
		mt(e, t), mt(e + "Capture", t);
	}
	function mt(e, t) {
		for (ft[e] = t, e = 0; e < t.length; e++) dt.add(t[e]);
	}
	var ht = RegExp("^[:A-Z_a-z\\u00C0-\\u00D6\\u00D8-\\u00F6\\u00F8-\\u02FF\\u0370-\\u037D\\u037F-\\u1FFF\\u200C-\\u200D\\u2070-\\u218F\\u2C00-\\u2FEF\\u3001-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFFD][:A-Z_a-z\\u00C0-\\u00D6\\u00D8-\\u00F6\\u00F8-\\u02FF\\u0370-\\u037D\\u037F-\\u1FFF\\u200C-\\u200D\\u2070-\\u218F\\u2C00-\\u2FEF\\u3001-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFFD\\-.0-9\\u00B7\\u0300-\\u036F\\u203F-\\u2040]*$"), gt = {}, _t = {};
	function vt(e) {
		return fe.call(_t, e) ? !0 : fe.call(gt, e) ? !1 : ht.test(e) ? _t[e] = !0 : (gt[e] = !0, !1);
	}
	function yt(e, t, n) {
		if (vt(t)) {
			if (n === null) e.removeAttribute(t);
			else {
				switch (typeof n) {
					case "undefined":
					case "function":
					case "symbol":
						e.removeAttribute(t);
						return;
					case "boolean":
						var r = t.toLowerCase().slice(0, 5);
						if (r !== "data-" && r !== "aria-") {
							e.removeAttribute(t);
							return;
						}
				}
				e.setAttribute(t, "" + n);
			}
		}
	}
	function bt(e, t, n) {
		if (n === null) e.removeAttribute(t);
		else {
			switch (typeof n) {
				case "undefined":
				case "function":
				case "symbol":
				case "boolean":
					e.removeAttribute(t);
					return;
			}
			e.setAttribute(t, "" + n);
		}
	}
	function xt(e, t, n, r) {
		if (r === null) e.removeAttribute(n);
		else {
			switch (typeof r) {
				case "undefined":
				case "function":
				case "symbol":
				case "boolean":
					e.removeAttribute(n);
					return;
			}
			e.setAttributeNS(t, n, "" + r);
		}
	}
	var St, Ct;
	function wt(e) {
		if (St === void 0) try {
			throw Error();
		} catch (e) {
			var t = e.stack.trim().match(/\n( *(at )?)/);
			St = t && t[1] || "", Ct = -1 < e.stack.indexOf("\n    at") ? " (<anonymous>)" : -1 < e.stack.indexOf("@") ? "@unknown:0:0" : "";
		}
		return "\n" + St + e + Ct;
	}
	var Tt = !1;
	function Et(e, t) {
		if (!e || Tt) return "";
		Tt = !0;
		var n = Error.prepareStackTrace;
		Error.prepareStackTrace = void 0;
		try {
			var r = { DetermineComponentFrameRoot: function() {
				try {
					if (t) {
						var n = function() {
							throw Error();
						};
						if (Object.defineProperty(n.prototype, "props", { set: function() {
							throw Error();
						} }), typeof Reflect == "object" && Reflect.construct) {
							try {
								Reflect.construct(n, []);
							} catch (e) {
								var r = e;
							}
							Reflect.construct(e, [], n);
						} else {
							try {
								n.call();
							} catch (e) {
								r = e;
							}
							e.call(n.prototype);
						}
					} else {
						try {
							throw Error();
						} catch (e) {
							r = e;
						}
						(n = e()) && typeof n.catch == "function" && n.catch(function() {});
					}
				} catch (e) {
					if (e && r && typeof e.stack == "string") return [e.stack, r.stack];
				}
				return [null, null];
			} };
			r.DetermineComponentFrameRoot.displayName = "DetermineComponentFrameRoot";
			var i = Object.getOwnPropertyDescriptor(r.DetermineComponentFrameRoot, "name");
			i && i.configurable && Object.defineProperty(r.DetermineComponentFrameRoot, "name", { value: "DetermineComponentFrameRoot" });
			var a = r.DetermineComponentFrameRoot(), o = a[0], s = a[1];
			if (o && s) {
				var c = o.split("\n"), l = s.split("\n");
				for (i = r = 0; r < c.length && !c[r].includes("DetermineComponentFrameRoot");) r++;
				for (; i < l.length && !l[i].includes("DetermineComponentFrameRoot");) i++;
				if (r === c.length || i === l.length) for (r = c.length - 1, i = l.length - 1; 1 <= r && 0 <= i && c[r] !== l[i];) i--;
				for (; 1 <= r && 0 <= i; r--, i--) if (c[r] !== l[i]) {
					if (r !== 1 || i !== 1) do
						if (r--, i--, 0 > i || c[r] !== l[i]) {
							var u = "\n" + c[r].replace(" at new ", " at ");
							return e.displayName && u.includes("<anonymous>") && (u = u.replace("<anonymous>", e.displayName)), u;
						}
					while (1 <= r && 0 <= i);
					break;
				}
			}
		} finally {
			Tt = !1, Error.prepareStackTrace = n;
		}
		return (n = e ? e.displayName || e.name : "") ? wt(n) : "";
	}
	function Dt(e) {
		switch (e.tag) {
			case 26:
			case 27:
			case 5: return wt(e.type);
			case 16: return wt("Lazy");
			case 13: return wt("Suspense");
			case 19: return wt("SuspenseList");
			case 0:
			case 15: return Et(e.type, !1);
			case 11: return Et(e.type.render, !1);
			case 1: return Et(e.type, !0);
			case 31: return wt("Activity");
			default: return "";
		}
	}
	function Ot(e) {
		try {
			var t = "";
			do
				t += Dt(e), e = e.return;
			while (e);
			return t;
		} catch (e) {
			return "\nError generating stack: " + e.message + "\n" + e.stack;
		}
	}
	function kt(e) {
		switch (typeof e) {
			case "bigint":
			case "boolean":
			case "number":
			case "string":
			case "undefined": return e;
			case "object": return e;
			default: return "";
		}
	}
	function At(e) {
		var t = e.type;
		return (e = e.nodeName) && e.toLowerCase() === "input" && (t === "checkbox" || t === "radio");
	}
	function jt(e) {
		var t = At(e) ? "checked" : "value", n = Object.getOwnPropertyDescriptor(e.constructor.prototype, t), r = "" + e[t];
		if (!e.hasOwnProperty(t) && n !== void 0 && typeof n.get == "function" && typeof n.set == "function") {
			var i = n.get, a = n.set;
			return Object.defineProperty(e, t, {
				configurable: !0,
				get: function() {
					return i.call(this);
				},
				set: function(e) {
					r = "" + e, a.call(this, e);
				}
			}), Object.defineProperty(e, t, { enumerable: n.enumerable }), {
				getValue: function() {
					return r;
				},
				setValue: function(e) {
					r = "" + e;
				},
				stopTracking: function() {
					e._valueTracker = null, delete e[t];
				}
			};
		}
	}
	function Mt(e) {
		e._valueTracker ||= jt(e);
	}
	function Nt(e) {
		if (!e) return !1;
		var t = e._valueTracker;
		if (!t) return !0;
		var n = t.getValue(), r = "";
		return e && (r = At(e) ? e.checked ? "true" : "false" : e.value), e = r, e !== n && (t.setValue(e), !0);
	}
	function Pt(e) {
		if (e ||= typeof document < "u" ? document : void 0, e === void 0) return null;
		try {
			return e.activeElement || e.body;
		} catch {
			return e.body;
		}
	}
	var Ft = /[\n"\\]/g;
	function It(e) {
		return e.replace(Ft, function(e) {
			return "\\" + e.charCodeAt(0).toString(16) + " ";
		});
	}
	function Lt(e, t, n, r, i, a, o, s) {
		e.name = "", o != null && typeof o != "function" && typeof o != "symbol" && typeof o != "boolean" ? e.type = o : e.removeAttribute("type"), t == null ? o !== "submit" && o !== "reset" || e.removeAttribute("value") : o === "number" ? (t === 0 && e.value === "" || e.value != t) && (e.value = "" + kt(t)) : e.value !== "" + kt(t) && (e.value = "" + kt(t)), t == null ? n == null ? r != null && e.removeAttribute("value") : zt(e, o, kt(n)) : zt(e, o, kt(t)), i == null && a != null && (e.defaultChecked = !!a), i != null && (e.checked = i && typeof i != "function" && typeof i != "symbol"), s != null && typeof s != "function" && typeof s != "symbol" && typeof s != "boolean" ? e.name = "" + kt(s) : e.removeAttribute("name");
	}
	function Rt(e, t, n, r, i, a, o, s) {
		if (a != null && typeof a != "function" && typeof a != "symbol" && typeof a != "boolean" && (e.type = a), t != null || n != null) {
			if (!(a !== "submit" && a !== "reset" || t != null)) return;
			n = n == null ? "" : "" + kt(n), t = t == null ? n : "" + kt(t), s || t === e.value || (e.value = t), e.defaultValue = t;
		}
		r ??= i, r = typeof r != "function" && typeof r != "symbol" && !!r, e.checked = s ? e.checked : !!r, e.defaultChecked = !!r, o != null && typeof o != "function" && typeof o != "symbol" && typeof o != "boolean" && (e.name = o);
	}
	function zt(e, t, n) {
		t === "number" && Pt(e.ownerDocument) === e || e.defaultValue === "" + n || (e.defaultValue = "" + n);
	}
	function Bt(e, t, n, r) {
		if (e = e.options, t) {
			t = {};
			for (var i = 0; i < n.length; i++) t["$" + n[i]] = !0;
			for (n = 0; n < e.length; n++) i = t.hasOwnProperty("$" + e[n].value), e[n].selected !== i && (e[n].selected = i), i && r && (e[n].defaultSelected = !0);
		} else {
			for (n = "" + kt(n), t = null, i = 0; i < e.length; i++) {
				if (e[i].value === n) {
					e[i].selected = !0, r && (e[i].defaultSelected = !0);
					return;
				}
				t !== null || e[i].disabled || (t = e[i]);
			}
			t !== null && (t.selected = !0);
		}
	}
	function Vt(e, t, n) {
		t != null && (t = "" + kt(t), t !== e.value && (e.value = t), n == null) ? e.defaultValue !== t && (e.defaultValue = t) : e.defaultValue = n == null ? "" : "" + kt(n);
	}
	function Ht(e, t, n, r) {
		if (t == null) {
			if (r != null) {
				if (n != null) throw Error(i(92));
				if (te(r)) {
					if (1 < r.length) throw Error(i(93));
					r = r[0];
				}
				n = r;
			}
			n ??= "", t = n;
		}
		n = kt(t), e.defaultValue = n, r = e.textContent, r === n && r !== "" && r !== null && (e.value = r);
	}
	function Ut(e, t) {
		if (t) {
			var n = e.firstChild;
			if (n && n === e.lastChild && n.nodeType === 3) {
				n.nodeValue = t;
				return;
			}
		}
		e.textContent = t;
	}
	var Wt = new Set("animationIterationCount aspectRatio borderImageOutset borderImageSlice borderImageWidth boxFlex boxFlexGroup boxOrdinalGroup columnCount columns flex flexGrow flexPositive flexShrink flexNegative flexOrder gridArea gridRow gridRowEnd gridRowSpan gridRowStart gridColumn gridColumnEnd gridColumnSpan gridColumnStart fontWeight lineClamp lineHeight opacity order orphans scale tabSize widows zIndex zoom fillOpacity floodOpacity stopOpacity strokeDasharray strokeDashoffset strokeMiterlimit strokeOpacity strokeWidth MozAnimationIterationCount MozBoxFlex MozBoxFlexGroup MozLineClamp msAnimationIterationCount msFlex msZoom msFlexGrow msFlexNegative msFlexOrder msFlexPositive msFlexShrink msGridColumn msGridColumnSpan msGridRow msGridRowSpan WebkitAnimationIterationCount WebkitBoxFlex WebKitBoxFlexGroup WebkitBoxOrdinalGroup WebkitColumnCount WebkitColumns WebkitFlex WebkitFlexGrow WebkitFlexPositive WebkitFlexShrink WebkitLineClamp".split(" "));
	function Gt(e, t, n) {
		var r = t.indexOf("--") === 0;
		n == null || typeof n == "boolean" || n === "" ? r ? e.setProperty(t, "") : t === "float" ? e.cssFloat = "" : e[t] = "" : r ? e.setProperty(t, n) : typeof n != "number" || n === 0 || Wt.has(t) ? t === "float" ? e.cssFloat = n : e[t] = ("" + n).trim() : e[t] = n + "px";
	}
	function Kt(e, t, n) {
		if (t != null && typeof t != "object") throw Error(i(62));
		if (e = e.style, n != null) {
			for (var r in n) !n.hasOwnProperty(r) || t != null && t.hasOwnProperty(r) || (r.indexOf("--") === 0 ? e.setProperty(r, "") : r === "float" ? e.cssFloat = "" : e[r] = "");
			for (var a in t) r = t[a], t.hasOwnProperty(a) && n[a] !== r && Gt(e, a, r);
		} else for (var o in t) t.hasOwnProperty(o) && Gt(e, o, t[o]);
	}
	function qt(e) {
		if (e.indexOf("-") === -1) return !1;
		switch (e) {
			case "annotation-xml":
			case "color-profile":
			case "font-face":
			case "font-face-src":
			case "font-face-uri":
			case "font-face-format":
			case "font-face-name":
			case "missing-glyph": return !1;
			default: return !0;
		}
	}
	var Jt = /* @__PURE__ */ new Map([
		["acceptCharset", "accept-charset"],
		["htmlFor", "for"],
		["httpEquiv", "http-equiv"],
		["crossOrigin", "crossorigin"],
		["accentHeight", "accent-height"],
		["alignmentBaseline", "alignment-baseline"],
		["arabicForm", "arabic-form"],
		["baselineShift", "baseline-shift"],
		["capHeight", "cap-height"],
		["clipPath", "clip-path"],
		["clipRule", "clip-rule"],
		["colorInterpolation", "color-interpolation"],
		["colorInterpolationFilters", "color-interpolation-filters"],
		["colorProfile", "color-profile"],
		["colorRendering", "color-rendering"],
		["dominantBaseline", "dominant-baseline"],
		["enableBackground", "enable-background"],
		["fillOpacity", "fill-opacity"],
		["fillRule", "fill-rule"],
		["floodColor", "flood-color"],
		["floodOpacity", "flood-opacity"],
		["fontFamily", "font-family"],
		["fontSize", "font-size"],
		["fontSizeAdjust", "font-size-adjust"],
		["fontStretch", "font-stretch"],
		["fontStyle", "font-style"],
		["fontVariant", "font-variant"],
		["fontWeight", "font-weight"],
		["glyphName", "glyph-name"],
		["glyphOrientationHorizontal", "glyph-orientation-horizontal"],
		["glyphOrientationVertical", "glyph-orientation-vertical"],
		["horizAdvX", "horiz-adv-x"],
		["horizOriginX", "horiz-origin-x"],
		["imageRendering", "image-rendering"],
		["letterSpacing", "letter-spacing"],
		["lightingColor", "lighting-color"],
		["markerEnd", "marker-end"],
		["markerMid", "marker-mid"],
		["markerStart", "marker-start"],
		["overlinePosition", "overline-position"],
		["overlineThickness", "overline-thickness"],
		["paintOrder", "paint-order"],
		["panose-1", "panose-1"],
		["pointerEvents", "pointer-events"],
		["renderingIntent", "rendering-intent"],
		["shapeRendering", "shape-rendering"],
		["stopColor", "stop-color"],
		["stopOpacity", "stop-opacity"],
		["strikethroughPosition", "strikethrough-position"],
		["strikethroughThickness", "strikethrough-thickness"],
		["strokeDasharray", "stroke-dasharray"],
		["strokeDashoffset", "stroke-dashoffset"],
		["strokeLinecap", "stroke-linecap"],
		["strokeLinejoin", "stroke-linejoin"],
		["strokeMiterlimit", "stroke-miterlimit"],
		["strokeOpacity", "stroke-opacity"],
		["strokeWidth", "stroke-width"],
		["textAnchor", "text-anchor"],
		["textDecoration", "text-decoration"],
		["textRendering", "text-rendering"],
		["transformOrigin", "transform-origin"],
		["underlinePosition", "underline-position"],
		["underlineThickness", "underline-thickness"],
		["unicodeBidi", "unicode-bidi"],
		["unicodeRange", "unicode-range"],
		["unitsPerEm", "units-per-em"],
		["vAlphabetic", "v-alphabetic"],
		["vHanging", "v-hanging"],
		["vIdeographic", "v-ideographic"],
		["vMathematical", "v-mathematical"],
		["vectorEffect", "vector-effect"],
		["vertAdvY", "vert-adv-y"],
		["vertOriginX", "vert-origin-x"],
		["vertOriginY", "vert-origin-y"],
		["wordSpacing", "word-spacing"],
		["writingMode", "writing-mode"],
		["xmlnsXlink", "xmlns:xlink"],
		["xHeight", "x-height"]
	]), Yt = /^[\u0000-\u001F ]*j[\r\n\t]*a[\r\n\t]*v[\r\n\t]*a[\r\n\t]*s[\r\n\t]*c[\r\n\t]*r[\r\n\t]*i[\r\n\t]*p[\r\n\t]*t[\r\n\t]*:/i;
	function Xt(e) {
		return Yt.test("" + e) ? "javascript:throw new Error('React has blocked a javascript: URL as a security precaution.')" : e;
	}
	var Zt = null;
	function Qt(e) {
		return e = e.target || e.srcElement || window, e.correspondingUseElement && (e = e.correspondingUseElement), e.nodeType === 3 ? e.parentNode : e;
	}
	var $t = null, en = null;
	function tn(e) {
		var t = st(e);
		if (t && (e = t.stateNode)) {
			var n = e[Qe] || null;
			a: switch (e = t.stateNode, t.type) {
				case "input":
					if (Lt(e, n.value, n.defaultValue, n.defaultValue, n.checked, n.defaultChecked, n.type, n.name), t = n.name, n.type === "radio" && t != null) {
						for (n = e; n.parentNode;) n = n.parentNode;
						for (n = n.querySelectorAll("input[name=\"" + It("" + t) + "\"][type=\"radio\"]"), t = 0; t < n.length; t++) {
							var r = n[t];
							if (r !== e && r.form === e.form) {
								var a = r[Qe] || null;
								if (!a) throw Error(i(90));
								Lt(r, a.value, a.defaultValue, a.defaultValue, a.checked, a.defaultChecked, a.type, a.name);
							}
						}
						for (t = 0; t < n.length; t++) r = n[t], r.form === e.form && Nt(r);
					}
					break a;
				case "textarea":
					Vt(e, n.value, n.defaultValue);
					break a;
				case "select": t = n.value, t != null && Bt(e, !!n.multiple, t, !1);
			}
		}
	}
	var nn = !1;
	function rn(e, t, n) {
		if (nn) return e(t, n);
		nn = !0;
		try {
			return e(t);
		} finally {
			if (nn = !1, ($t !== null || en !== null) && (cu(), $t && (t = $t, e = en, en = $t = null, tn(t), e))) for (t = 0; t < e.length; t++) tn(e[t]);
		}
	}
	function an(e, t) {
		var n = e.stateNode;
		if (n === null) return null;
		var r = n[Qe] || null;
		if (r === null) return null;
		n = r[t];
		a: switch (t) {
			case "onClick":
			case "onClickCapture":
			case "onDoubleClick":
			case "onDoubleClickCapture":
			case "onMouseDown":
			case "onMouseDownCapture":
			case "onMouseMove":
			case "onMouseMoveCapture":
			case "onMouseUp":
			case "onMouseUpCapture":
			case "onMouseEnter":
				(r = !r.disabled) || (e = e.type, r = e !== "button" && e !== "input" && e !== "select" && e !== "textarea"), e = !r;
				break a;
			default: e = !1;
		}
		if (e) return null;
		if (n && typeof n != "function") throw Error(i(231, t, typeof n));
		return n;
	}
	var on = typeof window < "u" && window.document !== void 0 && window.document.createElement !== void 0, sn = !1;
	if (on) try {
		var cn = {};
		Object.defineProperty(cn, "passive", { get: function() {
			sn = !0;
		} }), window.addEventListener("test", cn, cn), window.removeEventListener("test", cn, cn);
	} catch {
		sn = !1;
	}
	var ln = null, un = null, dn = null;
	function fn() {
		if (dn) return dn;
		var e, t = un, n = t.length, r, i = "value" in ln ? ln.value : ln.textContent, a = i.length;
		for (e = 0; e < n && t[e] === i[e]; e++);
		var o = n - e;
		for (r = 1; r <= o && t[n - r] === i[a - r]; r++);
		return dn = i.slice(e, 1 < r ? 1 - r : void 0);
	}
	function pn(e) {
		var t = e.keyCode;
		return "charCode" in e ? (e = e.charCode, e === 0 && t === 13 && (e = 13)) : e = t, e === 10 && (e = 13), 32 <= e || e === 13 ? e : 0;
	}
	function mn() {
		return !0;
	}
	function hn() {
		return !1;
	}
	function gn(e) {
		function t(t, n, r, i, a) {
			for (var o in this._reactName = t, this._targetInst = r, this.type = n, this.nativeEvent = i, this.target = a, this.currentTarget = null, e) e.hasOwnProperty(o) && (t = e[o], this[o] = t ? t(i) : i[o]);
			return this.isDefaultPrevented = (i.defaultPrevented == null ? !1 === i.returnValue : i.defaultPrevented) ? mn : hn, this.isPropagationStopped = hn, this;
		}
		return p(t.prototype, {
			preventDefault: function() {
				this.defaultPrevented = !0;
				var e = this.nativeEvent;
				e && (e.preventDefault ? e.preventDefault() : typeof e.returnValue != "unknown" && (e.returnValue = !1), this.isDefaultPrevented = mn);
			},
			stopPropagation: function() {
				var e = this.nativeEvent;
				e && (e.stopPropagation ? e.stopPropagation() : typeof e.cancelBubble != "unknown" && (e.cancelBubble = !0), this.isPropagationStopped = mn);
			},
			persist: function() {},
			isPersistent: mn
		}), t;
	}
	var _n = {
		eventPhase: 0,
		bubbles: 0,
		cancelable: 0,
		timeStamp: function(e) {
			return e.timeStamp || Date.now();
		},
		defaultPrevented: 0,
		isTrusted: 0
	}, vn = gn(_n), yn = p({}, _n, {
		view: 0,
		detail: 0
	}), bn = gn(yn), xn, Sn, Cn, wn = p({}, yn, {
		screenX: 0,
		screenY: 0,
		clientX: 0,
		clientY: 0,
		pageX: 0,
		pageY: 0,
		ctrlKey: 0,
		shiftKey: 0,
		altKey: 0,
		metaKey: 0,
		getModifierState: Fn,
		button: 0,
		buttons: 0,
		relatedTarget: function(e) {
			return e.relatedTarget === void 0 ? e.fromElement === e.srcElement ? e.toElement : e.fromElement : e.relatedTarget;
		},
		movementX: function(e) {
			return "movementX" in e ? e.movementX : (e !== Cn && (Cn && e.type === "mousemove" ? (xn = e.screenX - Cn.screenX, Sn = e.screenY - Cn.screenY) : Sn = xn = 0, Cn = e), xn);
		},
		movementY: function(e) {
			return "movementY" in e ? e.movementY : Sn;
		}
	}), Tn = gn(wn), En = gn(p({}, wn, { dataTransfer: 0 })), Dn = gn(p({}, yn, { relatedTarget: 0 })), On = gn(p({}, _n, {
		animationName: 0,
		elapsedTime: 0,
		pseudoElement: 0
	})), kn = gn(p({}, _n, { clipboardData: function(e) {
		return "clipboardData" in e ? e.clipboardData : window.clipboardData;
	} })), An = gn(p({}, _n, { data: 0 })), jn = {
		Esc: "Escape",
		Spacebar: " ",
		Left: "ArrowLeft",
		Up: "ArrowUp",
		Right: "ArrowRight",
		Down: "ArrowDown",
		Del: "Delete",
		Win: "OS",
		Menu: "ContextMenu",
		Apps: "ContextMenu",
		Scroll: "ScrollLock",
		MozPrintableKey: "Unidentified"
	}, Mn = {
		8: "Backspace",
		9: "Tab",
		12: "Clear",
		13: "Enter",
		16: "Shift",
		17: "Control",
		18: "Alt",
		19: "Pause",
		20: "CapsLock",
		27: "Escape",
		32: " ",
		33: "PageUp",
		34: "PageDown",
		35: "End",
		36: "Home",
		37: "ArrowLeft",
		38: "ArrowUp",
		39: "ArrowRight",
		40: "ArrowDown",
		45: "Insert",
		46: "Delete",
		112: "F1",
		113: "F2",
		114: "F3",
		115: "F4",
		116: "F5",
		117: "F6",
		118: "F7",
		119: "F8",
		120: "F9",
		121: "F10",
		122: "F11",
		123: "F12",
		144: "NumLock",
		145: "ScrollLock",
		224: "Meta"
	}, Nn = {
		Alt: "altKey",
		Control: "ctrlKey",
		Meta: "metaKey",
		Shift: "shiftKey"
	};
	function Pn(e) {
		var t = this.nativeEvent;
		return t.getModifierState ? t.getModifierState(e) : (e = Nn[e]) ? !!t[e] : !1;
	}
	function Fn() {
		return Pn;
	}
	var In = gn(p({}, yn, {
		key: function(e) {
			if (e.key) {
				var t = jn[e.key] || e.key;
				if (t !== "Unidentified") return t;
			}
			return e.type === "keypress" ? (e = pn(e), e === 13 ? "Enter" : String.fromCharCode(e)) : e.type === "keydown" || e.type === "keyup" ? Mn[e.keyCode] || "Unidentified" : "";
		},
		code: 0,
		location: 0,
		ctrlKey: 0,
		shiftKey: 0,
		altKey: 0,
		metaKey: 0,
		repeat: 0,
		locale: 0,
		getModifierState: Fn,
		charCode: function(e) {
			return e.type === "keypress" ? pn(e) : 0;
		},
		keyCode: function(e) {
			return e.type === "keydown" || e.type === "keyup" ? e.keyCode : 0;
		},
		which: function(e) {
			return e.type === "keypress" ? pn(e) : e.type === "keydown" || e.type === "keyup" ? e.keyCode : 0;
		}
	})), Ln = gn(p({}, wn, {
		pointerId: 0,
		width: 0,
		height: 0,
		pressure: 0,
		tangentialPressure: 0,
		tiltX: 0,
		tiltY: 0,
		twist: 0,
		pointerType: 0,
		isPrimary: 0
	})), Rn = gn(p({}, yn, {
		touches: 0,
		targetTouches: 0,
		changedTouches: 0,
		altKey: 0,
		metaKey: 0,
		ctrlKey: 0,
		shiftKey: 0,
		getModifierState: Fn
	})), zn = gn(p({}, _n, {
		propertyName: 0,
		elapsedTime: 0,
		pseudoElement: 0
	})), Bn = gn(p({}, wn, {
		deltaX: function(e) {
			return "deltaX" in e ? e.deltaX : "wheelDeltaX" in e ? -e.wheelDeltaX : 0;
		},
		deltaY: function(e) {
			return "deltaY" in e ? e.deltaY : "wheelDeltaY" in e ? -e.wheelDeltaY : "wheelDelta" in e ? -e.wheelDelta : 0;
		},
		deltaZ: 0,
		deltaMode: 0
	})), Vn = gn(p({}, _n, {
		newState: 0,
		oldState: 0
	})), Hn = [
		9,
		13,
		27,
		32
	], Un = on && "CompositionEvent" in window, Wn = null;
	on && "documentMode" in document && (Wn = document.documentMode);
	var Gn = on && "TextEvent" in window && !Wn, Kn = on && (!Un || Wn && 8 < Wn && 11 >= Wn), qn = " ", Jn = !1;
	function Yn(e, t) {
		switch (e) {
			case "keyup": return Hn.indexOf(t.keyCode) !== -1;
			case "keydown": return t.keyCode !== 229;
			case "keypress":
			case "mousedown":
			case "focusout": return !0;
			default: return !1;
		}
	}
	function Xn(e) {
		return e = e.detail, typeof e == "object" && "data" in e ? e.data : null;
	}
	var Zn = !1;
	function Qn(e, t) {
		switch (e) {
			case "compositionend": return Xn(t);
			case "keypress": return t.which === 32 ? (Jn = !0, qn) : null;
			case "textInput": return e = t.data, e === qn && Jn ? null : e;
			default: return null;
		}
	}
	function $n(e, t) {
		if (Zn) return e === "compositionend" || !Un && Yn(e, t) ? (e = fn(), dn = un = ln = null, Zn = !1, e) : null;
		switch (e) {
			case "paste": return null;
			case "keypress":
				if (!(t.ctrlKey || t.altKey || t.metaKey) || t.ctrlKey && t.altKey) {
					if (t.char && 1 < t.char.length) return t.char;
					if (t.which) return String.fromCharCode(t.which);
				}
				return null;
			case "compositionend": return Kn && t.locale !== "ko" ? null : t.data;
			default: return null;
		}
	}
	var er = {
		color: !0,
		date: !0,
		datetime: !0,
		"datetime-local": !0,
		email: !0,
		month: !0,
		number: !0,
		password: !0,
		range: !0,
		search: !0,
		tel: !0,
		text: !0,
		time: !0,
		url: !0,
		week: !0
	};
	function tr(e) {
		var t = e && e.nodeName && e.nodeName.toLowerCase();
		return t === "input" ? !!er[e.type] : t === "textarea";
	}
	function nr(e, t, n, r) {
		$t ? en ? en.push(r) : en = [r] : $t = r, t = pd(t, "onChange"), 0 < t.length && (n = new vn("onChange", "change", null, n, r), e.push({
			event: n,
			listeners: t
		}));
	}
	var rr = null, ir = null;
	function ar(e) {
		od(e, 0);
	}
	function or(e) {
		if (Nt(ct(e))) return e;
	}
	function sr(e, t) {
		if (e === "change") return t;
	}
	var cr = !1;
	if (on) {
		var lr;
		if (on) {
			var ur = "oninput" in document;
			if (!ur) {
				var dr = document.createElement("div");
				dr.setAttribute("oninput", "return;"), ur = typeof dr.oninput == "function";
			}
			lr = ur;
		} else lr = !1;
		cr = lr && (!document.documentMode || 9 < document.documentMode);
	}
	function fr() {
		rr && (rr.detachEvent("onpropertychange", pr), ir = rr = null);
	}
	function pr(e) {
		if (e.propertyName === "value" && or(ir)) {
			var t = [];
			nr(t, ir, e, Qt(e)), rn(ar, t);
		}
	}
	function mr(e, t, n) {
		e === "focusin" ? (fr(), rr = t, ir = n, rr.attachEvent("onpropertychange", pr)) : e === "focusout" && fr();
	}
	function hr(e) {
		if (e === "selectionchange" || e === "keyup" || e === "keydown") return or(ir);
	}
	function gr(e, t) {
		if (e === "click") return or(t);
	}
	function _r(e, t) {
		if (e === "input" || e === "change") return or(t);
	}
	function vr(e, t) {
		return e === t && (e !== 0 || 1 / e == 1 / t) || e !== e && t !== t;
	}
	var yr = typeof Object.is == "function" ? Object.is : vr;
	function br(e, t) {
		if (yr(e, t)) return !0;
		if (typeof e != "object" || !e || typeof t != "object" || !t) return !1;
		var n = Object.keys(e), r = Object.keys(t);
		if (n.length !== r.length) return !1;
		for (r = 0; r < n.length; r++) {
			var i = n[r];
			if (!fe.call(t, i) || !yr(e[i], t[i])) return !1;
		}
		return !0;
	}
	function xr(e) {
		for (; e && e.firstChild;) e = e.firstChild;
		return e;
	}
	function Sr(e, t) {
		var n = xr(e);
		e = 0;
		for (var r; n;) {
			if (n.nodeType === 3) {
				if (r = e + n.textContent.length, e <= t && r >= t) return {
					node: n,
					offset: t - e
				};
				e = r;
			}
			a: {
				for (; n;) {
					if (n.nextSibling) {
						n = n.nextSibling;
						break a;
					}
					n = n.parentNode;
				}
				n = void 0;
			}
			n = xr(n);
		}
	}
	function Cr(e, t) {
		return e && t ? e === t ? !0 : e && e.nodeType === 3 ? !1 : t && t.nodeType === 3 ? Cr(e, t.parentNode) : "contains" in e ? e.contains(t) : e.compareDocumentPosition ? !!(e.compareDocumentPosition(t) & 16) : !1 : !1;
	}
	function wr(e) {
		e = e != null && e.ownerDocument != null && e.ownerDocument.defaultView != null ? e.ownerDocument.defaultView : window;
		for (var t = Pt(e.document); t instanceof e.HTMLIFrameElement;) {
			try {
				var n = typeof t.contentWindow.location.href == "string";
			} catch {
				n = !1;
			}
			if (n) e = t.contentWindow;
			else break;
			t = Pt(e.document);
		}
		return t;
	}
	function Tr(e) {
		var t = e && e.nodeName && e.nodeName.toLowerCase();
		return t && (t === "input" && (e.type === "text" || e.type === "search" || e.type === "tel" || e.type === "url" || e.type === "password") || t === "textarea" || e.contentEditable === "true");
	}
	var Er = on && "documentMode" in document && 11 >= document.documentMode, Dr = null, Or = null, kr = null, Ar = !1;
	function jr(e, t, n) {
		var r = n.window === n ? n.document : n.nodeType === 9 ? n : n.ownerDocument;
		Ar || Dr == null || Dr !== Pt(r) || (r = Dr, "selectionStart" in r && Tr(r) ? r = {
			start: r.selectionStart,
			end: r.selectionEnd
		} : (r = (r.ownerDocument && r.ownerDocument.defaultView || window).getSelection(), r = {
			anchorNode: r.anchorNode,
			anchorOffset: r.anchorOffset,
			focusNode: r.focusNode,
			focusOffset: r.focusOffset
		}), kr && br(kr, r) || (kr = r, r = pd(Or, "onSelect"), 0 < r.length && (t = new vn("onSelect", "select", null, t, n), e.push({
			event: t,
			listeners: r
		}), t.target = Dr)));
	}
	function Mr(e, t) {
		var n = {};
		return n[e.toLowerCase()] = t.toLowerCase(), n["Webkit" + e] = "webkit" + t, n["Moz" + e] = "moz" + t, n;
	}
	var Nr = {
		animationend: Mr("Animation", "AnimationEnd"),
		animationiteration: Mr("Animation", "AnimationIteration"),
		animationstart: Mr("Animation", "AnimationStart"),
		transitionrun: Mr("Transition", "TransitionRun"),
		transitionstart: Mr("Transition", "TransitionStart"),
		transitioncancel: Mr("Transition", "TransitionCancel"),
		transitionend: Mr("Transition", "TransitionEnd")
	}, Pr = {}, Fr = {};
	on && (Fr = document.createElement("div").style, "AnimationEvent" in window || (delete Nr.animationend.animation, delete Nr.animationiteration.animation, delete Nr.animationstart.animation), "TransitionEvent" in window || delete Nr.transitionend.transition);
	function Ir(e) {
		if (Pr[e]) return Pr[e];
		if (!Nr[e]) return e;
		var t = Nr[e], n;
		for (n in t) if (t.hasOwnProperty(n) && n in Fr) return Pr[e] = t[n];
		return e;
	}
	var Lr = Ir("animationend"), Rr = Ir("animationiteration"), zr = Ir("animationstart"), Br = Ir("transitionrun"), Vr = Ir("transitionstart"), Hr = Ir("transitioncancel"), Ur = Ir("transitionend"), Wr = /* @__PURE__ */ new Map(), Gr = "abort auxClick beforeToggle cancel canPlay canPlayThrough click close contextMenu copy cut drag dragEnd dragEnter dragExit dragLeave dragOver dragStart drop durationChange emptied encrypted ended error gotPointerCapture input invalid keyDown keyPress keyUp load loadedData loadedMetadata loadStart lostPointerCapture mouseDown mouseMove mouseOut mouseOver mouseUp paste pause play playing pointerCancel pointerDown pointerMove pointerOut pointerOver pointerUp progress rateChange reset resize seeked seeking stalled submit suspend timeUpdate touchCancel touchEnd touchStart volumeChange scroll toggle touchMove waiting wheel".split(" ");
	Gr.push("scrollEnd");
	function Kr(e, t) {
		Wr.set(e, t), pt(t, [e]);
	}
	var qr = /* @__PURE__ */ new WeakMap();
	function Jr(e, t) {
		if (typeof e == "object" && e) {
			var n = qr.get(e);
			return n === void 0 ? (t = {
				value: e,
				source: t,
				stack: Ot(t)
			}, qr.set(e, t), t) : n;
		}
		return {
			value: e,
			source: t,
			stack: Ot(t)
		};
	}
	var Yr = [], Xr = 0, Zr = 0;
	function Qr() {
		for (var e = Xr, t = Zr = Xr = 0; t < e;) {
			var n = Yr[t];
			Yr[t++] = null;
			var r = Yr[t];
			Yr[t++] = null;
			var i = Yr[t];
			Yr[t++] = null;
			var a = Yr[t];
			if (Yr[t++] = null, r !== null && i !== null) {
				var o = r.pending;
				o === null ? i.next = i : (i.next = o.next, o.next = i), r.pending = i;
			}
			a !== 0 && ni(n, i, a);
		}
	}
	function $r(e, t, n, r) {
		Yr[Xr++] = e, Yr[Xr++] = t, Yr[Xr++] = n, Yr[Xr++] = r, Zr |= r, e.lanes |= r, e = e.alternate, e !== null && (e.lanes |= r);
	}
	function ei(e, t, n, r) {
		return $r(e, t, n, r), ri(e);
	}
	function ti(e, t) {
		return $r(e, null, null, t), ri(e);
	}
	function ni(e, t, n) {
		e.lanes |= n;
		var r = e.alternate;
		r !== null && (r.lanes |= n);
		for (var i = !1, a = e.return; a !== null;) a.childLanes |= n, r = a.alternate, r !== null && (r.childLanes |= n), a.tag === 22 && (e = a.stateNode, e === null || e._visibility & 1 || (i = !0)), e = a, a = a.return;
		return e.tag === 3 ? (a = e.stateNode, i && t !== null && (i = 31 - ke(n), e = a.hiddenUpdates, r = e[i], r === null ? e[i] = [t] : r.push(t), t.lane = n | 536870912), a) : null;
	}
	function ri(e) {
		if (50 < $l) throw $l = 0, eu = null, Error(i(185));
		for (var t = e.return; t !== null;) e = t, t = e.return;
		return e.tag === 3 ? e.stateNode : null;
	}
	var ii = {};
	function ai(e, t, n, r) {
		this.tag = e, this.key = n, this.sibling = this.child = this.return = this.stateNode = this.type = this.elementType = null, this.index = 0, this.refCleanup = this.ref = null, this.pendingProps = t, this.dependencies = this.memoizedState = this.updateQueue = this.memoizedProps = null, this.mode = r, this.subtreeFlags = this.flags = 0, this.deletions = null, this.childLanes = this.lanes = 0, this.alternate = null;
	}
	function oi(e, t, n, r) {
		return new ai(e, t, n, r);
	}
	function si(e) {
		return e = e.prototype, !(!e || !e.isReactComponent);
	}
	function ci(e, t) {
		var n = e.alternate;
		return n === null ? (n = oi(e.tag, t, e.key, e.mode), n.elementType = e.elementType, n.type = e.type, n.stateNode = e.stateNode, n.alternate = e, e.alternate = n) : (n.pendingProps = t, n.type = e.type, n.flags = 0, n.subtreeFlags = 0, n.deletions = null), n.flags = e.flags & 65011712, n.childLanes = e.childLanes, n.lanes = e.lanes, n.child = e.child, n.memoizedProps = e.memoizedProps, n.memoizedState = e.memoizedState, n.updateQueue = e.updateQueue, t = e.dependencies, n.dependencies = t === null ? null : {
			lanes: t.lanes,
			firstContext: t.firstContext
		}, n.sibling = e.sibling, n.index = e.index, n.ref = e.ref, n.refCleanup = e.refCleanup, n;
	}
	function li(e, t) {
		e.flags &= 65011714;
		var n = e.alternate;
		return n === null ? (e.childLanes = 0, e.lanes = t, e.child = null, e.subtreeFlags = 0, e.memoizedProps = null, e.memoizedState = null, e.updateQueue = null, e.dependencies = null, e.stateNode = null) : (e.childLanes = n.childLanes, e.lanes = n.lanes, e.child = n.child, e.subtreeFlags = 0, e.deletions = null, e.memoizedProps = n.memoizedProps, e.memoizedState = n.memoizedState, e.updateQueue = n.updateQueue, e.type = n.type, t = n.dependencies, e.dependencies = t === null ? null : {
			lanes: t.lanes,
			firstContext: t.firstContext
		}), e;
	}
	function ui(e, t, n, r, a, o) {
		var s = 0;
		if (r = e, typeof e == "function") si(e) && (s = 1);
		else if (typeof e == "string") s = Tf(e, n, ie.current) ? 26 : e === "html" || e === "head" || e === "body" ? 27 : 5;
		else a: switch (e) {
			case k: return e = oi(31, n, t, a), e.elementType = k, e.lanes = o, e;
			case v: return di(n.children, a, o, t);
			case y:
				s = 8, a |= 24;
				break;
			case b: return e = oi(12, n, t, a | 2), e.elementType = b, e.lanes = o, e;
			case T: return e = oi(13, n, t, a), e.elementType = T, e.lanes = o, e;
			case E: return e = oi(19, n, t, a), e.elementType = E, e.lanes = o, e;
			default:
				if (typeof e == "object" && e) switch (e.$$typeof) {
					case x:
					case C:
						s = 10;
						break a;
					case S:
						s = 9;
						break a;
					case w:
						s = 11;
						break a;
					case D:
						s = 14;
						break a;
					case O:
						s = 16, r = null;
						break a;
				}
				s = 29, n = Error(i(130, e === null ? "null" : typeof e, "")), r = null;
		}
		return t = oi(s, n, t, a), t.elementType = e, t.type = r, t.lanes = o, t;
	}
	function di(e, t, n, r) {
		return e = oi(7, e, r, t), e.lanes = n, e;
	}
	function fi(e, t, n) {
		return e = oi(6, e, null, t), e.lanes = n, e;
	}
	function pi(e, t, n) {
		return t = oi(4, e.children === null ? [] : e.children, e.key, t), t.lanes = n, t.stateNode = {
			containerInfo: e.containerInfo,
			pendingChildren: null,
			implementation: e.implementation
		}, t;
	}
	var mi = [], hi = 0, gi = null, _i = 0, vi = [], yi = 0, bi = null, xi = 1, Si = "";
	function Ci(e, t) {
		mi[hi++] = _i, mi[hi++] = gi, gi = e, _i = t;
	}
	function wi(e, t, n) {
		vi[yi++] = xi, vi[yi++] = Si, vi[yi++] = bi, bi = e;
		var r = xi;
		e = Si;
		var i = 32 - ke(r) - 1;
		r &= ~(1 << i), n += 1;
		var a = 32 - ke(t) + i;
		if (30 < a) {
			var o = i - i % 5;
			a = (r & (1 << o) - 1).toString(32), r >>= o, i -= o, xi = 1 << 32 - ke(t) + i | n << i | r, Si = a + e;
		} else xi = 1 << a | n << i | r, Si = e;
	}
	function Ti(e) {
		e.return !== null && (Ci(e, 1), wi(e, 1, 0));
	}
	function Ei(e) {
		for (; e === gi;) gi = mi[--hi], mi[hi] = null, _i = mi[--hi], mi[hi] = null;
		for (; e === bi;) bi = vi[--yi], vi[yi] = null, Si = vi[--yi], vi[yi] = null, xi = vi[--yi], vi[yi] = null;
	}
	var Di = null, Oi = null, B = !1, ki = null, Ai = !1, ji = Error(i(519));
	function Mi(e) {
		throw Ri(Jr(Error(i(418, "")), e)), ji;
	}
	function Ni(e) {
		var t = e.stateNode, n = e.type, r = e.memoizedProps;
		switch (t[Ze] = e, t[Qe] = r, n) {
			case "dialog":
				Q("cancel", t), Q("close", t);
				break;
			case "iframe":
			case "object":
			case "embed":
				Q("load", t);
				break;
			case "video":
			case "audio":
				for (n = 0; n < id.length; n++) Q(id[n], t);
				break;
			case "source":
				Q("error", t);
				break;
			case "img":
			case "image":
			case "link":
				Q("error", t), Q("load", t);
				break;
			case "details":
				Q("toggle", t);
				break;
			case "input":
				Q("invalid", t), Rt(t, r.value, r.defaultValue, r.checked, r.defaultChecked, r.type, r.name, !0), Mt(t);
				break;
			case "select":
				Q("invalid", t);
				break;
			case "textarea": Q("invalid", t), Ht(t, r.value, r.defaultValue, r.children), Mt(t);
		}
		n = r.children, typeof n != "string" && typeof n != "number" && typeof n != "bigint" || t.textContent === "" + n || !0 === r.suppressHydrationWarning || yd(t.textContent, n) ? (r.popover != null && (Q("beforetoggle", t), Q("toggle", t)), r.onScroll != null && Q("scroll", t), r.onScrollEnd != null && Q("scrollend", t), r.onClick != null && (t.onclick = bd), t = !0) : t = !1, t || Mi(e);
	}
	function Pi(e) {
		for (Di = e.return; Di;) switch (Di.tag) {
			case 5:
			case 13:
				Ai = !1;
				return;
			case 27:
			case 3:
				Ai = !0;
				return;
			default: Di = Di.return;
		}
	}
	function Fi(e) {
		if (e !== Di) return !1;
		if (!B) return Pi(e), B = !0, !1;
		var t = e.tag, n;
		if ((n = t !== 3 && t !== 27) && ((n = t === 5) && (n = e.type, n = n === "form" || n === "button" || kd(e.type, e.memoizedProps)), n = !n), n && Oi && Mi(e), Pi(e), t === 13) {
			if (e = e.memoizedState, e = e === null ? null : e.dehydrated, !e) throw Error(i(317));
			a: {
				for (e = e.nextSibling, t = 0; e;) {
					if (e.nodeType === 8) {
						if (n = e.data, n === "/$") {
							if (t === 0) {
								Oi = Wd(e.nextSibling);
								break a;
							}
							t--;
						} else n !== "$" && n !== "$!" && n !== "$?" || t++;
					}
					e = e.nextSibling;
				}
				Oi = null;
			}
		} else t === 27 ? (t = Oi, Ld(e.type) ? (e = Gd, Gd = null, Oi = e) : Oi = t) : Oi = Di ? Wd(e.stateNode.nextSibling) : null;
		return !0;
	}
	function Ii() {
		Oi = Di = null, B = !1;
	}
	function Li() {
		var e = ki;
		return e !== null && (Bl === null ? Bl = e : Bl.push.apply(Bl, e), ki = null), e;
	}
	function Ri(e) {
		ki === null ? ki = [e] : ki.push(e);
	}
	var zi = L(null), Bi = null, Vi = null;
	function Hi(e, t, n) {
		z(zi, t._currentValue), t._currentValue = n;
	}
	function Ui(e) {
		e._currentValue = zi.current, R(zi);
	}
	function Wi(e, t, n) {
		for (; e !== null;) {
			var r = e.alternate;
			if ((e.childLanes & t) === t ? r !== null && (r.childLanes & t) !== t && (r.childLanes |= t) : (e.childLanes |= t, r !== null && (r.childLanes |= t)), e === n) break;
			e = e.return;
		}
	}
	function Gi(e, t, n, r) {
		var a = e.child;
		for (a !== null && (a.return = e); a !== null;) {
			var o = a.dependencies;
			if (o !== null) {
				var s = a.child;
				o = o.firstContext;
				a: for (; o !== null;) {
					var c = o;
					o = a;
					for (var l = 0; l < t.length; l++) if (c.context === t[l]) {
						o.lanes |= n, c = o.alternate, c !== null && (c.lanes |= n), Wi(o.return, n, e), r || (s = null);
						break a;
					}
					o = c.next;
				}
			} else if (a.tag === 18) {
				if (s = a.return, s === null) throw Error(i(341));
				s.lanes |= n, o = s.alternate, o !== null && (o.lanes |= n), Wi(s, n, e), s = null;
			} else s = a.child;
			if (s !== null) s.return = a;
			else for (s = a; s !== null;) {
				if (s === e) {
					s = null;
					break;
				}
				if (a = s.sibling, a !== null) {
					a.return = s.return, s = a;
					break;
				}
				s = s.return;
			}
			a = s;
		}
	}
	function Ki(e, t, n, r) {
		e = null;
		for (var a = t, o = !1; a !== null;) {
			if (!o) {
				if (a.flags & 524288) o = !0;
				else if (a.flags & 262144) break;
			}
			if (a.tag === 10) {
				var s = a.alternate;
				if (s === null) throw Error(i(387));
				if (s = s.memoizedProps, s !== null) {
					var c = a.type;
					yr(a.pendingProps.value, s.value) || (e === null ? e = [c] : e.push(c));
				}
			} else if (a === se.current) {
				if (s = a.alternate, s === null) throw Error(i(387));
				s.memoizedState.memoizedState !== a.memoizedState.memoizedState && (e === null ? e = [Ff] : e.push(Ff));
			}
			a = a.return;
		}
		e !== null && Gi(t, e, n, r), t.flags |= 262144;
	}
	function qi(e) {
		for (e = e.firstContext; e !== null;) {
			if (!yr(e.context._currentValue, e.memoizedValue)) return !0;
			e = e.next;
		}
		return !1;
	}
	function Ji(e) {
		Bi = e, Vi = null, e = e.dependencies, e !== null && (e.firstContext = null);
	}
	function Yi(e) {
		return Zi(Bi, e);
	}
	function Xi(e, t) {
		return Bi === null && Ji(e), Zi(e, t);
	}
	function Zi(e, t) {
		var n = t._currentValue;
		if (t = {
			context: t,
			memoizedValue: n,
			next: null
		}, Vi === null) {
			if (e === null) throw Error(i(308));
			Vi = t, e.dependencies = {
				lanes: 0,
				firstContext: t
			}, e.flags |= 524288;
		} else Vi = Vi.next = t;
		return n;
	}
	var Qi = typeof AbortController < "u" ? AbortController : function() {
		var e = [], t = this.signal = {
			aborted: !1,
			addEventListener: function(t, n) {
				e.push(n);
			}
		};
		this.abort = function() {
			t.aborted = !0, e.forEach(function(e) {
				return e();
			});
		};
	}, $i = t.unstable_scheduleCallback, ea = t.unstable_NormalPriority, ta = {
		$$typeof: C,
		Consumer: null,
		Provider: null,
		_currentValue: null,
		_currentValue2: null,
		_threadCount: 0
	};
	function na() {
		return {
			controller: new Qi(),
			data: /* @__PURE__ */ new Map(),
			refCount: 0
		};
	}
	function ra(e) {
		e.refCount--, e.refCount === 0 && $i(ea, function() {
			e.controller.abort();
		});
	}
	var ia = null, aa = 0, oa = 0, sa = null;
	function ca(e, t) {
		if (ia === null) {
			var n = ia = [];
			aa = 0, oa = Qu(), sa = {
				status: "pending",
				value: void 0,
				then: function(e) {
					n.push(e);
				}
			};
		}
		return aa++, t.then(la, la), t;
	}
	function la() {
		if (--aa === 0 && ia !== null) {
			sa !== null && (sa.status = "fulfilled");
			var e = ia;
			ia = null, oa = 0, sa = null;
			for (var t = 0; t < e.length; t++) (0, e[t])();
		}
	}
	function ua(e, t) {
		var n = [], r = {
			status: "pending",
			value: null,
			reason: null,
			then: function(e) {
				n.push(e);
			}
		};
		return e.then(function() {
			r.status = "fulfilled", r.value = t;
			for (var e = 0; e < n.length; e++) (0, n[e])(t);
		}, function(e) {
			for (r.status = "rejected", r.reason = e, e = 0; e < n.length; e++) (0, n[e])(void 0);
		}), r;
	}
	var da = P.S;
	P.S = function(e, t) {
		typeof t == "object" && t && typeof t.then == "function" && ca(e, t), da !== null && da(e, t);
	};
	var fa = L(null);
	function pa() {
		var e = fa.current;
		return e === null ? q.pooledCache : e;
	}
	function ma(e, t) {
		t === null ? z(fa, fa.current) : z(fa, t.pool);
	}
	function ha() {
		var e = pa();
		return e === null ? null : {
			parent: ta._currentValue,
			pool: e
		};
	}
	var ga = Error(i(460)), _a = Error(i(474)), va = Error(i(542)), ya = { then: function() {} };
	function ba(e) {
		return e = e.status, e === "fulfilled" || e === "rejected";
	}
	function xa() {}
	function Sa(e, t, n) {
		switch (n = e[n], n === void 0 ? e.push(t) : n !== t && (t.then(xa, xa), t = n), t.status) {
			case "fulfilled": return t.value;
			case "rejected": throw e = t.reason, Ta(e), e;
			default:
				if (typeof t.status == "string") t.then(xa, xa);
				else {
					if (e = q, e !== null && 100 < e.shellSuspendCounter) throw Error(i(482));
					e = t, e.status = "pending", e.then(function(e) {
						if (t.status === "pending") {
							var n = t;
							n.status = "fulfilled", n.value = e;
						}
					}, function(e) {
						if (t.status === "pending") {
							var n = t;
							n.status = "rejected", n.reason = e;
						}
					});
				}
				switch (t.status) {
					case "fulfilled": return t.value;
					case "rejected": throw e = t.reason, Ta(e), e;
				}
				throw Ca = t, ga;
		}
	}
	var Ca = null;
	function wa() {
		if (Ca === null) throw Error(i(459));
		var e = Ca;
		return Ca = null, e;
	}
	function Ta(e) {
		if (e === ga || e === va) throw Error(i(483));
	}
	var Ea = !1;
	function Da(e) {
		e.updateQueue = {
			baseState: e.memoizedState,
			firstBaseUpdate: null,
			lastBaseUpdate: null,
			shared: {
				pending: null,
				lanes: 0,
				hiddenCallbacks: null
			},
			callbacks: null
		};
	}
	function Oa(e, t) {
		e = e.updateQueue, t.updateQueue === e && (t.updateQueue = {
			baseState: e.baseState,
			firstBaseUpdate: e.firstBaseUpdate,
			lastBaseUpdate: e.lastBaseUpdate,
			shared: e.shared,
			callbacks: null
		});
	}
	function V(e) {
		return {
			lane: e,
			tag: 0,
			payload: null,
			callback: null,
			next: null
		};
	}
	function ka(e, t, n) {
		var r = e.updateQueue;
		if (r === null) return null;
		if (r = r.shared, K & 2) {
			var i = r.pending;
			return i === null ? t.next = t : (t.next = i.next, i.next = t), r.pending = t, t = ri(e), ni(e, null, n), t;
		}
		return $r(e, r, t, n), ri(e);
	}
	function Aa(e, t, n) {
		if (t = t.updateQueue, t !== null && (t = t.shared, n & 4194048)) {
			var r = t.lanes;
			r &= e.pendingLanes, n |= r, t.lanes = n, Ge(e, n);
		}
	}
	function ja(e, t) {
		var n = e.updateQueue, r = e.alternate;
		if (r !== null && (r = r.updateQueue, n === r)) {
			var i = null, a = null;
			if (n = n.firstBaseUpdate, n !== null) {
				do {
					var o = {
						lane: n.lane,
						tag: n.tag,
						payload: n.payload,
						callback: null,
						next: null
					};
					a === null ? i = a = o : a = a.next = o, n = n.next;
				} while (n !== null);
				a === null ? i = a = t : a = a.next = t;
			} else i = a = t;
			n = {
				baseState: r.baseState,
				firstBaseUpdate: i,
				lastBaseUpdate: a,
				shared: r.shared,
				callbacks: r.callbacks
			}, e.updateQueue = n;
		} else e = n.lastBaseUpdate, e === null ? n.firstBaseUpdate = t : e.next = t, n.lastBaseUpdate = t;
	}
	var Ma = !1;
	function Na() {
		if (Ma) {
			var e = sa;
			if (e !== null) throw e;
		}
	}
	function Pa(e, t, n, r) {
		Ma = !1;
		var i = e.updateQueue;
		Ea = !1;
		var a = i.firstBaseUpdate, o = i.lastBaseUpdate, s = i.shared.pending;
		if (s !== null) {
			i.shared.pending = null;
			var c = s, l = c.next;
			c.next = null, o === null ? a = l : o.next = l, o = c;
			var u = e.alternate;
			u !== null && (u = u.updateQueue, s = u.lastBaseUpdate, s !== o && (s === null ? u.firstBaseUpdate = l : s.next = l, u.lastBaseUpdate = c));
		}
		if (a !== null) {
			var d = i.baseState;
			o = 0, u = l = c = null, s = a;
			do {
				var f = s.lane & -536870913, m = f !== s.lane;
				if (m ? (Y & f) === f : (r & f) === f) {
					f !== 0 && f === oa && (Ma = !0), u !== null && (u = u.next = {
						lane: 0,
						tag: s.tag,
						payload: s.payload,
						callback: null,
						next: null
					});
					a: {
						var h = e, g = s;
						f = t;
						var _ = n;
						switch (g.tag) {
							case 1:
								if (h = g.payload, typeof h == "function") {
									d = h.call(_, d, f);
									break a;
								}
								d = h;
								break a;
							case 3: h.flags = h.flags & -65537 | 128;
							case 0:
								if (h = g.payload, f = typeof h == "function" ? h.call(_, d, f) : h, f == null) break a;
								d = p({}, d, f);
								break a;
							case 2: Ea = !0;
						}
					}
					f = s.callback, f !== null && (e.flags |= 64, m && (e.flags |= 8192), m = i.callbacks, m === null ? i.callbacks = [f] : m.push(f));
				} else m = {
					lane: f,
					tag: s.tag,
					payload: s.payload,
					callback: s.callback,
					next: null
				}, u === null ? (l = u = m, c = d) : u = u.next = m, o |= f;
				if (s = s.next, s === null) {
					if (s = i.shared.pending, s === null) break;
					m = s, s = m.next, m.next = null, i.lastBaseUpdate = m, i.shared.pending = null;
				}
			} while (1);
			u === null && (c = d), i.baseState = c, i.firstBaseUpdate = l, i.lastBaseUpdate = u, a === null && (i.shared.lanes = 0), Pl |= o, e.lanes = o, e.memoizedState = d;
		}
	}
	function Fa(e, t) {
		if (typeof e != "function") throw Error(i(191, e));
		e.call(t);
	}
	function Ia(e, t) {
		var n = e.callbacks;
		if (n !== null) for (e.callbacks = null, e = 0; e < n.length; e++) Fa(n[e], t);
	}
	var La = L(null), Ra = L(0);
	function za(e, t) {
		e = Ml, z(Ra, e), z(La, t), Ml = e | t.baseLanes;
	}
	function Ba() {
		z(Ra, Ml), z(La, La.current);
	}
	function Va() {
		Ml = Ra.current, R(La), R(Ra);
	}
	var Ha = 0, H = null, U = null, Ua = null, Wa = !1, Ga = !1, Ka = !1, qa = 0, Ja = 0, Ya = null, Xa = 0;
	function Za() {
		throw Error(i(321));
	}
	function Qa(e, t) {
		if (t === null) return !1;
		for (var n = 0; n < t.length && n < e.length; n++) if (!yr(e[n], t[n])) return !1;
		return !0;
	}
	function $a(e, t, n, r, i, a) {
		return Ha = a, H = t, t.memoizedState = null, t.updateQueue = null, t.lanes = 0, P.H = e === null || e.memoizedState === null ? gs : _s, Ka = !1, a = n(r, i), Ka = !1, Ga && (a = to(t, n, r, i)), eo(e), a;
	}
	function eo(e) {
		P.H = hs;
		var t = U !== null && U.next !== null;
		if (Ha = 0, Ua = U = H = null, Wa = !1, Ja = 0, Ya = null, t) throw Error(i(300));
		e === null || Qs || (e = e.dependencies, e !== null && qi(e) && (Qs = !0));
	}
	function to(e, t, n, r) {
		H = e;
		var a = 0;
		do {
			if (Ga && (Ya = null), Ja = 0, Ga = !1, 25 <= a) throw Error(i(301));
			if (a += 1, Ua = U = null, e.updateQueue != null) {
				var o = e.updateQueue;
				o.lastEffect = null, o.events = null, o.stores = null, o.memoCache != null && (o.memoCache.index = 0);
			}
			P.H = vs, o = t(n, r);
		} while (Ga);
		return o;
	}
	function no() {
		var e = P.H, t = e.useState()[0];
		return t = typeof t.then == "function" ? lo(t) : t, e = e.useState()[0], (U === null ? null : U.memoizedState) !== e && (H.flags |= 1024), t;
	}
	function ro() {
		var e = qa !== 0;
		return qa = 0, e;
	}
	function io(e, t, n) {
		t.updateQueue = e.updateQueue, t.flags &= -2053, e.lanes &= ~n;
	}
	function ao(e) {
		if (Wa) {
			for (e = e.memoizedState; e !== null;) {
				var t = e.queue;
				t !== null && (t.pending = null), e = e.next;
			}
			Wa = !1;
		}
		Ha = 0, Ua = U = H = null, Ga = !1, Ja = qa = 0, Ya = null;
	}
	function oo() {
		var e = {
			memoizedState: null,
			baseState: null,
			baseQueue: null,
			queue: null,
			next: null
		};
		return Ua === null ? H.memoizedState = Ua = e : Ua = Ua.next = e, Ua;
	}
	function so() {
		if (U === null) {
			var e = H.alternate;
			e = e === null ? null : e.memoizedState;
		} else e = U.next;
		var t = Ua === null ? H.memoizedState : Ua.next;
		if (t !== null) Ua = t, U = e;
		else {
			if (e === null) throw H.alternate === null ? Error(i(467)) : Error(i(310));
			U = e, e = {
				memoizedState: U.memoizedState,
				baseState: U.baseState,
				baseQueue: U.baseQueue,
				queue: U.queue,
				next: null
			}, Ua === null ? H.memoizedState = Ua = e : Ua = Ua.next = e;
		}
		return Ua;
	}
	function co() {
		return {
			lastEffect: null,
			events: null,
			stores: null,
			memoCache: null
		};
	}
	function lo(e) {
		var t = Ja;
		return Ja += 1, Ya === null && (Ya = []), e = Sa(Ya, e, t), t = H, (Ua === null ? t.memoizedState : Ua.next) === null && (t = t.alternate, P.H = t === null || t.memoizedState === null ? gs : _s), e;
	}
	function uo(e) {
		if (typeof e == "object" && e) {
			if (typeof e.then == "function") return lo(e);
			if (e.$$typeof === C) return Yi(e);
		}
		throw Error(i(438, String(e)));
	}
	function fo(e) {
		var t = null, n = H.updateQueue;
		if (n !== null && (t = n.memoCache), t == null) {
			var r = H.alternate;
			r !== null && (r = r.updateQueue, r !== null && (r = r.memoCache, r != null && (t = {
				data: r.data.map(function(e) {
					return e.slice();
				}),
				index: 0
			})));
		}
		if (t ??= {
			data: [],
			index: 0
		}, n === null && (n = co(), H.updateQueue = n), n.memoCache = t, n = t.data[t.index], n === void 0) for (n = t.data[t.index] = Array(e), r = 0; r < e; r++) n[r] = A;
		return t.index++, n;
	}
	function po(e, t) {
		return typeof t == "function" ? t(e) : t;
	}
	function mo(e) {
		return ho(so(), U, e);
	}
	function ho(e, t, n) {
		var r = e.queue;
		if (r === null) throw Error(i(311));
		r.lastRenderedReducer = n;
		var a = e.baseQueue, o = r.pending;
		if (o !== null) {
			if (a !== null) {
				var s = a.next;
				a.next = o.next, o.next = s;
			}
			t.baseQueue = a = o, r.pending = null;
		}
		if (o = e.baseState, a === null) e.memoizedState = o;
		else {
			t = a.next;
			var c = s = null, l = null, u = t, d = !1;
			do {
				var f = u.lane & -536870913;
				if (f === u.lane ? (Ha & f) === f : (Y & f) === f) {
					var p = u.revertLane;
					if (p === 0) l !== null && (l = l.next = {
						lane: 0,
						revertLane: 0,
						action: u.action,
						hasEagerState: u.hasEagerState,
						eagerState: u.eagerState,
						next: null
					}), f === oa && (d = !0);
					else if ((Ha & p) === p) {
						u = u.next, p === oa && (d = !0);
						continue;
					} else f = {
						lane: 0,
						revertLane: u.revertLane,
						action: u.action,
						hasEagerState: u.hasEagerState,
						eagerState: u.eagerState,
						next: null
					}, l === null ? (c = l = f, s = o) : l = l.next = f, H.lanes |= p, Pl |= p;
					f = u.action, Ka && n(o, f), o = u.hasEagerState ? u.eagerState : n(o, f);
				} else p = {
					lane: f,
					revertLane: u.revertLane,
					action: u.action,
					hasEagerState: u.hasEagerState,
					eagerState: u.eagerState,
					next: null
				}, l === null ? (c = l = p, s = o) : l = l.next = p, H.lanes |= f, Pl |= f;
				u = u.next;
			} while (u !== null && u !== t);
			if (l === null ? s = o : l.next = c, !yr(o, e.memoizedState) && (Qs = !0, d && (n = sa, n !== null))) throw n;
			e.memoizedState = o, e.baseState = s, e.baseQueue = l, r.lastRenderedState = o;
		}
		return a === null && (r.lanes = 0), [e.memoizedState, r.dispatch];
	}
	function go(e) {
		var t = so(), n = t.queue;
		if (n === null) throw Error(i(311));
		n.lastRenderedReducer = e;
		var r = n.dispatch, a = n.pending, o = t.memoizedState;
		if (a !== null) {
			n.pending = null;
			var s = a = a.next;
			do
				o = e(o, s.action), s = s.next;
			while (s !== a);
			yr(o, t.memoizedState) || (Qs = !0), t.memoizedState = o, t.baseQueue === null && (t.baseState = o), n.lastRenderedState = o;
		}
		return [o, r];
	}
	function _o(e, t, n) {
		var r = H, a = so(), o = B;
		if (o) {
			if (n === void 0) throw Error(i(407));
			n = n();
		} else n = t();
		var s = !yr((U || a).memoizedState, n);
		if (s && (a.memoizedState = n, Qs = !0), a = a.queue, Vo(2048, 8, bo.bind(null, r, a, e), [e]), a.getSnapshot !== t || s || Ua !== null && Ua.memoizedState.tag & 1) {
			if (r.flags |= 2048, Lo(9, Ro(), yo.bind(null, r, a, n, t), null), q === null) throw Error(i(349));
			o || Ha & 124 || vo(r, t, n);
		}
		return n;
	}
	function vo(e, t, n) {
		e.flags |= 16384, e = {
			getSnapshot: t,
			value: n
		}, t = H.updateQueue, t === null ? (t = co(), H.updateQueue = t, t.stores = [e]) : (n = t.stores, n === null ? t.stores = [e] : n.push(e));
	}
	function yo(e, t, n, r) {
		t.value = n, t.getSnapshot = r, xo(t) && So(e);
	}
	function bo(e, t, n) {
		return n(function() {
			xo(t) && So(e);
		});
	}
	function xo(e) {
		var t = e.getSnapshot;
		e = e.value;
		try {
			var n = t();
			return !yr(e, n);
		} catch {
			return !0;
		}
	}
	function So(e) {
		var t = ti(e, 2);
		t !== null && ru(t, e, 2);
	}
	function Co(e) {
		var t = oo();
		if (typeof e == "function") {
			var n = e;
			if (e = n(), Ka) {
				Oe(!0);
				try {
					n();
				} finally {
					Oe(!1);
				}
			}
		}
		return t.memoizedState = t.baseState = e, t.queue = {
			pending: null,
			lanes: 0,
			dispatch: null,
			lastRenderedReducer: po,
			lastRenderedState: e
		}, t;
	}
	function wo(e, t, n, r) {
		return e.baseState = n, ho(e, U, typeof r == "function" ? r : po);
	}
	function To(e, t, n, r, a) {
		if (fs(e)) throw Error(i(485));
		if (e = t.action, e !== null) {
			var o = {
				payload: a,
				action: e,
				next: null,
				isTransition: !0,
				status: "pending",
				value: null,
				reason: null,
				listeners: [],
				then: function(e) {
					o.listeners.push(e);
				}
			};
			P.T === null ? o.isTransition = !1 : n(!0), r(o), n = t.pending, n === null ? (o.next = t.pending = o, Eo(t, o)) : (o.next = n.next, t.pending = n.next = o);
		}
	}
	function Eo(e, t) {
		var n = t.action, r = t.payload, i = e.state;
		if (t.isTransition) {
			var a = P.T, o = {};
			P.T = o;
			try {
				var s = n(i, r), c = P.S;
				c !== null && c(o, s), Do(e, t, s);
			} catch (n) {
				ko(e, t, n);
			} finally {
				P.T = a;
			}
		} else try {
			a = n(i, r), Do(e, t, a);
		} catch (n) {
			ko(e, t, n);
		}
	}
	function Do(e, t, n) {
		typeof n == "object" && n && typeof n.then == "function" ? n.then(function(n) {
			Oo(e, t, n);
		}, function(n) {
			return ko(e, t, n);
		}) : Oo(e, t, n);
	}
	function Oo(e, t, n) {
		t.status = "fulfilled", t.value = n, Ao(t), e.state = n, t = e.pending, t !== null && (n = t.next, n === t ? e.pending = null : (n = n.next, t.next = n, Eo(e, n)));
	}
	function ko(e, t, n) {
		var r = e.pending;
		if (e.pending = null, r !== null) {
			r = r.next;
			do
				t.status = "rejected", t.reason = n, Ao(t), t = t.next;
			while (t !== r);
		}
		e.action = null;
	}
	function Ao(e) {
		e = e.listeners;
		for (var t = 0; t < e.length; t++) (0, e[t])();
	}
	function jo(e, t) {
		return t;
	}
	function Mo(e, t) {
		if (B) {
			var n = q.formState;
			if (n !== null) {
				a: {
					var r = H;
					if (B) {
						if (Oi) {
							b: {
								for (var i = Oi, a = Ai; i.nodeType !== 8;) if (!a || (i = Wd(i.nextSibling), i === null)) {
									i = null;
									break b;
								}
								a = i.data, i = a === "F!" || a === "F" ? i : null;
							}
							if (i) {
								Oi = Wd(i.nextSibling), r = i.data === "F!";
								break a;
							}
						}
						Mi(r);
					}
					r = !1;
				}
				r && (t = n[0]);
			}
		}
		return n = oo(), n.memoizedState = n.baseState = t, r = {
			pending: null,
			lanes: 0,
			dispatch: null,
			lastRenderedReducer: jo,
			lastRenderedState: t
		}, n.queue = r, n = ls.bind(null, H, r), r.dispatch = n, r = Co(!1), a = ds.bind(null, H, !1, r.queue), r = oo(), i = {
			state: t,
			dispatch: null,
			action: e,
			pending: null
		}, r.queue = i, n = To.bind(null, H, i, a, n), i.dispatch = n, r.memoizedState = e, [
			t,
			n,
			!1
		];
	}
	function No(e) {
		return Po(so(), U, e);
	}
	function Po(e, t, n) {
		if (t = ho(e, t, jo)[0], e = mo(po)[0], typeof t == "object" && t && typeof t.then == "function") try {
			var r = lo(t);
		} catch (e) {
			throw e === ga ? va : e;
		}
		else r = t;
		t = so();
		var i = t.queue, a = i.dispatch;
		return n !== t.memoizedState && (H.flags |= 2048, Lo(9, Ro(), Fo.bind(null, i, n), null)), [
			r,
			a,
			e
		];
	}
	function Fo(e, t) {
		e.action = t;
	}
	function Io(e) {
		var t = so(), n = U;
		if (n !== null) return Po(t, n, e);
		so(), t = t.memoizedState, n = so();
		var r = n.queue.dispatch;
		return n.memoizedState = e, [
			t,
			r,
			!1
		];
	}
	function Lo(e, t, n, r) {
		return e = {
			tag: e,
			create: n,
			deps: r,
			inst: t,
			next: null
		}, t = H.updateQueue, t === null && (t = co(), H.updateQueue = t), n = t.lastEffect, n === null ? t.lastEffect = e.next = e : (r = n.next, n.next = e, e.next = r, t.lastEffect = e), e;
	}
	function Ro() {
		return {
			destroy: void 0,
			resource: void 0
		};
	}
	function zo() {
		return so().memoizedState;
	}
	function Bo(e, t, n, r) {
		var i = oo();
		r = r === void 0 ? null : r, H.flags |= e, i.memoizedState = Lo(1 | t, Ro(), n, r);
	}
	function Vo(e, t, n, r) {
		var i = so();
		r = r === void 0 ? null : r;
		var a = i.memoizedState.inst;
		U !== null && r !== null && Qa(r, U.memoizedState.deps) ? i.memoizedState = Lo(t, a, n, r) : (H.flags |= e, i.memoizedState = Lo(1 | t, a, n, r));
	}
	function Ho(e, t) {
		Bo(8390656, 8, e, t);
	}
	function Uo(e, t) {
		Vo(2048, 8, e, t);
	}
	function Wo(e, t) {
		return Vo(4, 2, e, t);
	}
	function Go(e, t) {
		return Vo(4, 4, e, t);
	}
	function Ko(e, t) {
		if (typeof t == "function") {
			e = e();
			var n = t(e);
			return function() {
				typeof n == "function" ? n() : t(null);
			};
		}
		if (t != null) return e = e(), t.current = e, function() {
			t.current = null;
		};
	}
	function qo(e, t, n) {
		n = n == null ? null : n.concat([e]), Vo(4, 4, Ko.bind(null, t, e), n);
	}
	function Jo() {}
	function Yo(e, t) {
		var n = so();
		t = t === void 0 ? null : t;
		var r = n.memoizedState;
		return t !== null && Qa(t, r[1]) ? r[0] : (n.memoizedState = [e, t], e);
	}
	function Xo(e, t) {
		var n = so();
		t = t === void 0 ? null : t;
		var r = n.memoizedState;
		if (t !== null && Qa(t, r[1])) return r[0];
		if (r = e(), Ka) {
			Oe(!0);
			try {
				e();
			} finally {
				Oe(!1);
			}
		}
		return n.memoizedState = [r, t], r;
	}
	function Zo(e, t, n) {
		return n === void 0 || Ha & 1073741824 ? e.memoizedState = t : (e.memoizedState = n, e = nu(), H.lanes |= e, Pl |= e, n);
	}
	function Qo(e, t, n, r) {
		return yr(n, t) ? n : La.current === null ? Ha & 42 ? (e = nu(), H.lanes |= e, Pl |= e, t) : (Qs = !0, e.memoizedState = n) : (e = Zo(e, n, r), yr(e, t) || (Qs = !0), e);
	}
	function $o(e, t, n, r, i) {
		var a = F.p;
		F.p = a !== 0 && 8 > a ? a : 8;
		var o = P.T, s = {};
		P.T = s, ds(e, !1, t, n);
		try {
			var c = i(), l = P.S;
			l !== null && l(s, c), typeof c == "object" && c && typeof c.then == "function" ? us(e, t, ua(c, r), tu(e)) : us(e, t, r, tu(e));
		} catch (n) {
			us(e, t, {
				then: function() {},
				status: "rejected",
				reason: n
			}, tu());
		} finally {
			F.p = a, P.T = o;
		}
	}
	function es() {}
	function ts(e, t, n, r) {
		if (e.tag !== 5) throw Error(i(476));
		var a = ns(e).queue;
		$o(e, a, t, ne, n === null ? es : function() {
			return rs(e), n(r);
		});
	}
	function ns(e) {
		var t = e.memoizedState;
		if (t !== null) return t;
		t = {
			memoizedState: ne,
			baseState: ne,
			baseQueue: null,
			queue: {
				pending: null,
				lanes: 0,
				dispatch: null,
				lastRenderedReducer: po,
				lastRenderedState: ne
			},
			next: null
		};
		var n = {};
		return t.next = {
			memoizedState: n,
			baseState: n,
			baseQueue: null,
			queue: {
				pending: null,
				lanes: 0,
				dispatch: null,
				lastRenderedReducer: po,
				lastRenderedState: n
			},
			next: null
		}, e.memoizedState = t, e = e.alternate, e !== null && (e.memoizedState = t), t;
	}
	function rs(e) {
		var t = ns(e).next.queue;
		us(e, t, {}, tu());
	}
	function is() {
		return Yi(Ff);
	}
	function as() {
		return so().memoizedState;
	}
	function os() {
		return so().memoizedState;
	}
	function ss(e) {
		for (var t = e.return; t !== null;) {
			switch (t.tag) {
				case 24:
				case 3:
					var n = tu();
					e = V(n);
					var r = ka(t, e, n);
					r !== null && (ru(r, t, n), Aa(r, t, n)), t = { cache: na() }, e.payload = t;
					return;
			}
			t = t.return;
		}
	}
	function cs(e, t, n) {
		var r = tu();
		n = {
			lane: r,
			revertLane: 0,
			action: n,
			hasEagerState: !1,
			eagerState: null,
			next: null
		}, fs(e) ? ps(t, n) : (n = ei(e, t, n, r), n !== null && (ru(n, e, r), ms(n, t, r)));
	}
	function ls(e, t, n) {
		us(e, t, n, tu());
	}
	function us(e, t, n, r) {
		var i = {
			lane: r,
			revertLane: 0,
			action: n,
			hasEagerState: !1,
			eagerState: null,
			next: null
		};
		if (fs(e)) ps(t, i);
		else {
			var a = e.alternate;
			if (e.lanes === 0 && (a === null || a.lanes === 0) && (a = t.lastRenderedReducer, a !== null)) try {
				var o = t.lastRenderedState, s = a(o, n);
				if (i.hasEagerState = !0, i.eagerState = s, yr(s, o)) return $r(e, t, i, 0), q === null && Qr(), !1;
			} catch {}
			if (n = ei(e, t, i, r), n !== null) return ru(n, e, r), ms(n, t, r), !0;
		}
		return !1;
	}
	function ds(e, t, n, r) {
		if (r = {
			lane: 2,
			revertLane: Qu(),
			action: r,
			hasEagerState: !1,
			eagerState: null,
			next: null
		}, fs(e)) {
			if (t) throw Error(i(479));
		} else t = ei(e, n, r, 2), t !== null && ru(t, e, 2);
	}
	function fs(e) {
		var t = e.alternate;
		return e === H || t !== null && t === H;
	}
	function ps(e, t) {
		Ga = Wa = !0;
		var n = e.pending;
		n === null ? t.next = t : (t.next = n.next, n.next = t), e.pending = t;
	}
	function ms(e, t, n) {
		if (n & 4194048) {
			var r = t.lanes;
			r &= e.pendingLanes, n |= r, t.lanes = n, Ge(e, n);
		}
	}
	var hs = {
		readContext: Yi,
		use: uo,
		useCallback: Za,
		useContext: Za,
		useEffect: Za,
		useImperativeHandle: Za,
		useLayoutEffect: Za,
		useInsertionEffect: Za,
		useMemo: Za,
		useReducer: Za,
		useRef: Za,
		useState: Za,
		useDebugValue: Za,
		useDeferredValue: Za,
		useTransition: Za,
		useSyncExternalStore: Za,
		useId: Za,
		useHostTransitionStatus: Za,
		useFormState: Za,
		useActionState: Za,
		useOptimistic: Za,
		useMemoCache: Za,
		useCacheRefresh: Za
	}, gs = {
		readContext: Yi,
		use: uo,
		useCallback: function(e, t) {
			return oo().memoizedState = [e, t === void 0 ? null : t], e;
		},
		useContext: Yi,
		useEffect: Ho,
		useImperativeHandle: function(e, t, n) {
			n = n == null ? null : n.concat([e]), Bo(4194308, 4, Ko.bind(null, t, e), n);
		},
		useLayoutEffect: function(e, t) {
			return Bo(4194308, 4, e, t);
		},
		useInsertionEffect: function(e, t) {
			Bo(4, 2, e, t);
		},
		useMemo: function(e, t) {
			var n = oo();
			t = t === void 0 ? null : t;
			var r = e();
			if (Ka) {
				Oe(!0);
				try {
					e();
				} finally {
					Oe(!1);
				}
			}
			return n.memoizedState = [r, t], r;
		},
		useReducer: function(e, t, n) {
			var r = oo();
			if (n !== void 0) {
				var i = n(t);
				if (Ka) {
					Oe(!0);
					try {
						n(t);
					} finally {
						Oe(!1);
					}
				}
			} else i = t;
			return r.memoizedState = r.baseState = i, e = {
				pending: null,
				lanes: 0,
				dispatch: null,
				lastRenderedReducer: e,
				lastRenderedState: i
			}, r.queue = e, e = e.dispatch = cs.bind(null, H, e), [r.memoizedState, e];
		},
		useRef: function(e) {
			var t = oo();
			return e = { current: e }, t.memoizedState = e;
		},
		useState: function(e) {
			e = Co(e);
			var t = e.queue, n = ls.bind(null, H, t);
			return t.dispatch = n, [e.memoizedState, n];
		},
		useDebugValue: Jo,
		useDeferredValue: function(e, t) {
			return Zo(oo(), e, t);
		},
		useTransition: function() {
			var e = Co(!1);
			return e = $o.bind(null, H, e.queue, !0, !1), oo().memoizedState = e, [!1, e];
		},
		useSyncExternalStore: function(e, t, n) {
			var r = H, a = oo();
			if (B) {
				if (n === void 0) throw Error(i(407));
				n = n();
			} else {
				if (n = t(), q === null) throw Error(i(349));
				Y & 124 || vo(r, t, n);
			}
			a.memoizedState = n;
			var o = {
				value: n,
				getSnapshot: t
			};
			return a.queue = o, Ho(bo.bind(null, r, o, e), [e]), r.flags |= 2048, Lo(9, Ro(), yo.bind(null, r, o, n, t), null), n;
		},
		useId: function() {
			var e = oo(), t = q.identifierPrefix;
			if (B) {
				var n = Si, r = xi;
				n = (r & ~(1 << 32 - ke(r) - 1)).toString(32) + n, t = "«" + t + "R" + n, n = qa++, 0 < n && (t += "H" + n.toString(32)), t += "»";
			} else n = Xa++, t = "«" + t + "r" + n.toString(32) + "»";
			return e.memoizedState = t;
		},
		useHostTransitionStatus: is,
		useFormState: Mo,
		useActionState: Mo,
		useOptimistic: function(e) {
			var t = oo();
			t.memoizedState = t.baseState = e;
			var n = {
				pending: null,
				lanes: 0,
				dispatch: null,
				lastRenderedReducer: null,
				lastRenderedState: null
			};
			return t.queue = n, t = ds.bind(null, H, !0, n), n.dispatch = t, [e, t];
		},
		useMemoCache: fo,
		useCacheRefresh: function() {
			return oo().memoizedState = ss.bind(null, H);
		}
	}, _s = {
		readContext: Yi,
		use: uo,
		useCallback: Yo,
		useContext: Yi,
		useEffect: Uo,
		useImperativeHandle: qo,
		useInsertionEffect: Wo,
		useLayoutEffect: Go,
		useMemo: Xo,
		useReducer: mo,
		useRef: zo,
		useState: function() {
			return mo(po);
		},
		useDebugValue: Jo,
		useDeferredValue: function(e, t) {
			return Qo(so(), U.memoizedState, e, t);
		},
		useTransition: function() {
			var e = mo(po)[0], t = so().memoizedState;
			return [typeof e == "boolean" ? e : lo(e), t];
		},
		useSyncExternalStore: _o,
		useId: as,
		useHostTransitionStatus: is,
		useFormState: No,
		useActionState: No,
		useOptimistic: function(e, t) {
			return wo(so(), U, e, t);
		},
		useMemoCache: fo,
		useCacheRefresh: os
	}, vs = {
		readContext: Yi,
		use: uo,
		useCallback: Yo,
		useContext: Yi,
		useEffect: Uo,
		useImperativeHandle: qo,
		useInsertionEffect: Wo,
		useLayoutEffect: Go,
		useMemo: Xo,
		useReducer: go,
		useRef: zo,
		useState: function() {
			return go(po);
		},
		useDebugValue: Jo,
		useDeferredValue: function(e, t) {
			var n = so();
			return U === null ? Zo(n, e, t) : Qo(n, U.memoizedState, e, t);
		},
		useTransition: function() {
			var e = go(po)[0], t = so().memoizedState;
			return [typeof e == "boolean" ? e : lo(e), t];
		},
		useSyncExternalStore: _o,
		useId: as,
		useHostTransitionStatus: is,
		useFormState: Io,
		useActionState: Io,
		useOptimistic: function(e, t) {
			var n = so();
			return U === null ? (n.baseState = e, [e, n.queue.dispatch]) : wo(n, U, e, t);
		},
		useMemoCache: fo,
		useCacheRefresh: os
	}, ys = null, bs = 0;
	function xs(e) {
		var t = bs;
		return bs += 1, ys === null && (ys = []), Sa(ys, e, t);
	}
	function Ss(e, t) {
		t = t.props.ref, e.ref = t === void 0 ? null : t;
	}
	function Cs(e, t) {
		throw t.$$typeof === h ? Error(i(525)) : (e = Object.prototype.toString.call(t), Error(i(31, e === "[object Object]" ? "object with keys {" + Object.keys(t).join(", ") + "}" : e)));
	}
	function ws(e) {
		var t = e._init;
		return t(e._payload);
	}
	function Ts(e) {
		function t(t, n) {
			if (e) {
				var r = t.deletions;
				r === null ? (t.deletions = [n], t.flags |= 16) : r.push(n);
			}
		}
		function n(n, r) {
			if (!e) return null;
			for (; r !== null;) t(n, r), r = r.sibling;
			return null;
		}
		function r(e) {
			for (var t = /* @__PURE__ */ new Map(); e !== null;) e.key === null ? t.set(e.index, e) : t.set(e.key, e), e = e.sibling;
			return t;
		}
		function a(e, t) {
			return e = ci(e, t), e.index = 0, e.sibling = null, e;
		}
		function o(t, n, r) {
			return t.index = r, e ? (r = t.alternate, r === null ? (t.flags |= 67108866, n) : (r = r.index, r < n ? (t.flags |= 67108866, n) : r)) : (t.flags |= 1048576, n);
		}
		function s(t) {
			return e && t.alternate === null && (t.flags |= 67108866), t;
		}
		function c(e, t, n, r) {
			return t === null || t.tag !== 6 ? (t = fi(n, e.mode, r), t.return = e, t) : (t = a(t, n), t.return = e, t);
		}
		function l(e, t, n, r) {
			var i = n.type;
			return i === v ? d(e, t, n.props.children, r, n.key) : t !== null && (t.elementType === i || typeof i == "object" && i && i.$$typeof === O && ws(i) === t.type) ? (t = a(t, n.props), Ss(t, n), t.return = e, t) : (t = ui(n.type, n.key, n.props, null, e.mode, r), Ss(t, n), t.return = e, t);
		}
		function u(e, t, n, r) {
			return t === null || t.tag !== 4 || t.stateNode.containerInfo !== n.containerInfo || t.stateNode.implementation !== n.implementation ? (t = pi(n, e.mode, r), t.return = e, t) : (t = a(t, n.children || []), t.return = e, t);
		}
		function d(e, t, n, r, i) {
			return t === null || t.tag !== 7 ? (t = di(n, e.mode, r, i), t.return = e, t) : (t = a(t, n), t.return = e, t);
		}
		function f(e, t, n) {
			if (typeof t == "string" && t !== "" || typeof t == "number" || typeof t == "bigint") return t = fi("" + t, e.mode, n), t.return = e, t;
			if (typeof t == "object" && t) {
				switch (t.$$typeof) {
					case g: return n = ui(t.type, t.key, t.props, null, e.mode, n), Ss(n, t), n.return = e, n;
					case _: return t = pi(t, e.mode, n), t.return = e, t;
					case O:
						var r = t._init;
						return t = r(t._payload), f(e, t, n);
				}
				if (te(t) || M(t)) return t = di(t, e.mode, n, null), t.return = e, t;
				if (typeof t.then == "function") return f(e, xs(t), n);
				if (t.$$typeof === C) return f(e, Xi(e, t), n);
				Cs(e, t);
			}
			return null;
		}
		function p(e, t, n, r) {
			var i = t === null ? null : t.key;
			if (typeof n == "string" && n !== "" || typeof n == "number" || typeof n == "bigint") return i === null ? c(e, t, "" + n, r) : null;
			if (typeof n == "object" && n) {
				switch (n.$$typeof) {
					case g: return n.key === i ? l(e, t, n, r) : null;
					case _: return n.key === i ? u(e, t, n, r) : null;
					case O: return i = n._init, n = i(n._payload), p(e, t, n, r);
				}
				if (te(n) || M(n)) return i === null ? d(e, t, n, r, null) : null;
				if (typeof n.then == "function") return p(e, t, xs(n), r);
				if (n.$$typeof === C) return p(e, t, Xi(e, n), r);
				Cs(e, n);
			}
			return null;
		}
		function m(e, t, n, r, i) {
			if (typeof r == "string" && r !== "" || typeof r == "number" || typeof r == "bigint") return e = e.get(n) || null, c(t, e, "" + r, i);
			if (typeof r == "object" && r) {
				switch (r.$$typeof) {
					case g: return e = e.get(r.key === null ? n : r.key) || null, l(t, e, r, i);
					case _: return e = e.get(r.key === null ? n : r.key) || null, u(t, e, r, i);
					case O:
						var a = r._init;
						return r = a(r._payload), m(e, t, n, r, i);
				}
				if (te(r) || M(r)) return e = e.get(n) || null, d(t, e, r, i, null);
				if (typeof r.then == "function") return m(e, t, n, xs(r), i);
				if (r.$$typeof === C) return m(e, t, n, Xi(t, r), i);
				Cs(t, r);
			}
			return null;
		}
		function h(i, a, s, c) {
			for (var l = null, u = null, d = a, h = a = 0, g = null; d !== null && h < s.length; h++) {
				d.index > h ? (g = d, d = null) : g = d.sibling;
				var _ = p(i, d, s[h], c);
				if (_ === null) {
					d === null && (d = g);
					break;
				}
				e && d && _.alternate === null && t(i, d), a = o(_, a, h), u === null ? l = _ : u.sibling = _, u = _, d = g;
			}
			if (h === s.length) return n(i, d), B && Ci(i, h), l;
			if (d === null) {
				for (; h < s.length; h++) d = f(i, s[h], c), d !== null && (a = o(d, a, h), u === null ? l = d : u.sibling = d, u = d);
				return B && Ci(i, h), l;
			}
			for (d = r(d); h < s.length; h++) g = m(d, i, h, s[h], c), g !== null && (e && g.alternate !== null && d.delete(g.key === null ? h : g.key), a = o(g, a, h), u === null ? l = g : u.sibling = g, u = g);
			return e && d.forEach(function(e) {
				return t(i, e);
			}), B && Ci(i, h), l;
		}
		function y(a, s, c, l) {
			if (c == null) throw Error(i(151));
			for (var u = null, d = null, h = s, g = s = 0, _ = null, v = c.next(); h !== null && !v.done; g++, v = c.next()) {
				h.index > g ? (_ = h, h = null) : _ = h.sibling;
				var y = p(a, h, v.value, l);
				if (y === null) {
					h === null && (h = _);
					break;
				}
				e && h && y.alternate === null && t(a, h), s = o(y, s, g), d === null ? u = y : d.sibling = y, d = y, h = _;
			}
			if (v.done) return n(a, h), B && Ci(a, g), u;
			if (h === null) {
				for (; !v.done; g++, v = c.next()) v = f(a, v.value, l), v !== null && (s = o(v, s, g), d === null ? u = v : d.sibling = v, d = v);
				return B && Ci(a, g), u;
			}
			for (h = r(h); !v.done; g++, v = c.next()) v = m(h, a, g, v.value, l), v !== null && (e && v.alternate !== null && h.delete(v.key === null ? g : v.key), s = o(v, s, g), d === null ? u = v : d.sibling = v, d = v);
			return e && h.forEach(function(e) {
				return t(a, e);
			}), B && Ci(a, g), u;
		}
		function b(e, r, o, c) {
			if (typeof o == "object" && o && o.type === v && o.key === null && (o = o.props.children), typeof o == "object" && o) {
				switch (o.$$typeof) {
					case g:
						a: {
							for (var l = o.key; r !== null;) {
								if (r.key === l) {
									if (l = o.type, l === v) {
										if (r.tag === 7) {
											n(e, r.sibling), c = a(r, o.props.children), c.return = e, e = c;
											break a;
										}
									} else if (r.elementType === l || typeof l == "object" && l && l.$$typeof === O && ws(l) === r.type) {
										n(e, r.sibling), c = a(r, o.props), Ss(c, o), c.return = e, e = c;
										break a;
									}
									n(e, r);
									break;
								}
								t(e, r), r = r.sibling;
							}
							o.type === v ? (c = di(o.props.children, e.mode, c, o.key), c.return = e, e = c) : (c = ui(o.type, o.key, o.props, null, e.mode, c), Ss(c, o), c.return = e, e = c);
						}
						return s(e);
					case _:
						a: {
							for (l = o.key; r !== null;) {
								if (r.key === l) {
									if (r.tag === 4 && r.stateNode.containerInfo === o.containerInfo && r.stateNode.implementation === o.implementation) {
										n(e, r.sibling), c = a(r, o.children || []), c.return = e, e = c;
										break a;
									}
									n(e, r);
									break;
								}
								t(e, r), r = r.sibling;
							}
							c = pi(o, e.mode, c), c.return = e, e = c;
						}
						return s(e);
					case O: return l = o._init, o = l(o._payload), b(e, r, o, c);
				}
				if (te(o)) return h(e, r, o, c);
				if (M(o)) {
					if (l = M(o), typeof l != "function") throw Error(i(150));
					return o = l.call(o), y(e, r, o, c);
				}
				if (typeof o.then == "function") return b(e, r, xs(o), c);
				if (o.$$typeof === C) return b(e, r, Xi(e, o), c);
				Cs(e, o);
			}
			return typeof o == "string" && o !== "" || typeof o == "number" || typeof o == "bigint" ? (o = "" + o, r !== null && r.tag === 6 ? (n(e, r.sibling), c = a(r, o), c.return = e, e = c) : (n(e, r), c = fi(o, e.mode, c), c.return = e, e = c), s(e)) : n(e, r);
		}
		return function(e, t, n, r) {
			try {
				bs = 0;
				var i = b(e, t, n, r);
				return ys = null, i;
			} catch (t) {
				if (t === ga || t === va) throw t;
				var a = oi(29, t, null, e.mode);
				return a.lanes = r, a.return = e, a;
			}
		};
	}
	var Es = Ts(!0), Ds = Ts(!1), Os = L(null), ks = null;
	function As(e) {
		var t = e.alternate;
		z(Ps, Ps.current & 1), z(Os, e), ks === null && (t === null || La.current !== null || t.memoizedState !== null) && (ks = e);
	}
	function js(e) {
		if (e.tag === 22) {
			if (z(Ps, Ps.current), z(Os, e), ks === null) {
				var t = e.alternate;
				t !== null && t.memoizedState !== null && (ks = e);
			}
		} else Ms(e);
	}
	function Ms() {
		z(Ps, Ps.current), z(Os, Os.current);
	}
	function Ns(e) {
		R(Os), ks === e && (ks = null), R(Ps);
	}
	var Ps = L(0);
	function Fs(e) {
		for (var t = e; t !== null;) {
			if (t.tag === 13) {
				var n = t.memoizedState;
				if (n !== null && (n = n.dehydrated, n === null || n.data === "$?" || Hd(n))) return t;
			} else if (t.tag === 19 && t.memoizedProps.revealOrder !== void 0) {
				if (t.flags & 128) return t;
			} else if (t.child !== null) {
				t.child.return = t, t = t.child;
				continue;
			}
			if (t === e) break;
			for (; t.sibling === null;) {
				if (t.return === null || t.return === e) return null;
				t = t.return;
			}
			t.sibling.return = t.return, t = t.sibling;
		}
		return null;
	}
	function Is(e, t, n, r) {
		t = e.memoizedState, n = n(r, t), n = n == null ? t : p({}, t, n), e.memoizedState = n, e.lanes === 0 && (e.updateQueue.baseState = n);
	}
	var Ls = {
		enqueueSetState: function(e, t, n) {
			e = e._reactInternals;
			var r = tu(), i = V(r);
			i.payload = t, n != null && (i.callback = n), t = ka(e, i, r), t !== null && (ru(t, e, r), Aa(t, e, r));
		},
		enqueueReplaceState: function(e, t, n) {
			e = e._reactInternals;
			var r = tu(), i = V(r);
			i.tag = 1, i.payload = t, n != null && (i.callback = n), t = ka(e, i, r), t !== null && (ru(t, e, r), Aa(t, e, r));
		},
		enqueueForceUpdate: function(e, t) {
			e = e._reactInternals;
			var n = tu(), r = V(n);
			r.tag = 2, t != null && (r.callback = t), t = ka(e, r, n), t !== null && (ru(t, e, n), Aa(t, e, n));
		}
	};
	function Rs(e, t, n, r, i, a, o) {
		return e = e.stateNode, typeof e.shouldComponentUpdate == "function" ? e.shouldComponentUpdate(r, a, o) : t.prototype && t.prototype.isPureReactComponent ? !br(n, r) || !br(i, a) : !0;
	}
	function zs(e, t, n, r) {
		e = t.state, typeof t.componentWillReceiveProps == "function" && t.componentWillReceiveProps(n, r), typeof t.UNSAFE_componentWillReceiveProps == "function" && t.UNSAFE_componentWillReceiveProps(n, r), t.state !== e && Ls.enqueueReplaceState(t, t.state, null);
	}
	function Bs(e, t) {
		var n = t;
		if ("ref" in t) for (var r in n = {}, t) r !== "ref" && (n[r] = t[r]);
		if (e = e.defaultProps) for (var i in n === t && (n = p({}, n)), e) n[i] === void 0 && (n[i] = e[i]);
		return n;
	}
	var Vs = typeof reportError == "function" ? reportError : function(e) {
		if (typeof window == "object" && typeof window.ErrorEvent == "function") {
			var t = new window.ErrorEvent("error", {
				bubbles: !0,
				cancelable: !0,
				message: typeof e == "object" && e && typeof e.message == "string" ? String(e.message) : String(e),
				error: e
			});
			if (!window.dispatchEvent(t)) return;
		} else if (typeof process == "object" && typeof process.emit == "function") {
			process.emit("uncaughtException", e);
			return;
		}
		console.error(e);
	};
	function Hs(e) {
		Vs(e);
	}
	function Us(e) {
		console.error(e);
	}
	function Ws(e) {
		Vs(e);
	}
	function Gs(e, t) {
		try {
			var n = e.onUncaughtError;
			n(t.value, { componentStack: t.stack });
		} catch (e) {
			setTimeout(function() {
				throw e;
			});
		}
	}
	function Ks(e, t, n) {
		try {
			var r = e.onCaughtError;
			r(n.value, {
				componentStack: n.stack,
				errorBoundary: t.tag === 1 ? t.stateNode : null
			});
		} catch (e) {
			setTimeout(function() {
				throw e;
			});
		}
	}
	function qs(e, t, n) {
		return n = V(n), n.tag = 3, n.payload = { element: null }, n.callback = function() {
			Gs(e, t);
		}, n;
	}
	function Js(e) {
		return e = V(e), e.tag = 3, e;
	}
	function Ys(e, t, n, r) {
		var i = n.type.getDerivedStateFromError;
		if (typeof i == "function") {
			var a = r.value;
			e.payload = function() {
				return i(a);
			}, e.callback = function() {
				Ks(t, n, r);
			};
		}
		var o = n.stateNode;
		o !== null && typeof o.componentDidCatch == "function" && (e.callback = function() {
			Ks(t, n, r), typeof i != "function" && (Gl === null ? Gl = /* @__PURE__ */ new Set([this]) : Gl.add(this));
			var e = r.stack;
			this.componentDidCatch(r.value, { componentStack: e === null ? "" : e });
		});
	}
	function Xs(e, t, n, r, a) {
		if (n.flags |= 32768, typeof r == "object" && r && typeof r.then == "function") {
			if (t = n.alternate, t !== null && Ki(t, n, a, !0), n = Os.current, n !== null) {
				switch (n.tag) {
					case 13: return ks === null ? mu() : n.alternate === null && Nl === 0 && (Nl = 3), n.flags &= -257, n.flags |= 65536, n.lanes = a, r === ya ? n.flags |= 16384 : (t = n.updateQueue, t === null ? n.updateQueue = /* @__PURE__ */ new Set([r]) : t.add(r), Mu(e, r, a)), !1;
					case 22: return n.flags |= 65536, r === ya ? n.flags |= 16384 : (t = n.updateQueue, t === null ? (t = {
						transitions: null,
						markerInstances: null,
						retryQueue: /* @__PURE__ */ new Set([r])
					}, n.updateQueue = t) : (n = t.retryQueue, n === null ? t.retryQueue = /* @__PURE__ */ new Set([r]) : n.add(r)), Mu(e, r, a)), !1;
				}
				throw Error(i(435, n.tag));
			}
			return Mu(e, r, a), mu(), !1;
		}
		if (B) return t = Os.current, t === null ? (r !== ji && (t = Error(i(423), { cause: r }), Ri(Jr(t, n))), e = e.current.alternate, e.flags |= 65536, a &= -a, e.lanes |= a, r = Jr(r, n), a = qs(e.stateNode, r, a), ja(e, a), Nl !== 4 && (Nl = 2)) : (!(t.flags & 65536) && (t.flags |= 256), t.flags |= 65536, t.lanes = a, r !== ji && (e = Error(i(422), { cause: r }), Ri(Jr(e, n)))), !1;
		var o = Error(i(520), { cause: r });
		if (o = Jr(o, n), zl === null ? zl = [o] : zl.push(o), Nl !== 4 && (Nl = 2), t === null) return !0;
		r = Jr(r, n), n = t;
		do {
			switch (n.tag) {
				case 3: return n.flags |= 65536, e = a & -a, n.lanes |= e, e = qs(n.stateNode, r, e), ja(n, e), !1;
				case 1: if (t = n.type, o = n.stateNode, !(n.flags & 128) && (typeof t.getDerivedStateFromError == "function" || o !== null && typeof o.componentDidCatch == "function" && (Gl === null || !Gl.has(o)))) return n.flags |= 65536, a &= -a, n.lanes |= a, a = Js(a), Ys(a, e, n, r), ja(n, a), !1;
			}
			n = n.return;
		} while (n !== null);
		return !1;
	}
	var Zs = Error(i(461)), Qs = !1;
	function $s(e, t, n, r) {
		t.child = e === null ? Ds(t, null, n, r) : Es(t, e.child, n, r);
	}
	function ec(e, t, n, r, i) {
		n = n.render;
		var a = t.ref;
		if ("ref" in r) {
			var o = {};
			for (var s in r) s !== "ref" && (o[s] = r[s]);
		} else o = r;
		return Ji(t), r = $a(e, t, n, o, a, i), s = ro(), e !== null && !Qs ? (io(e, t, i), bc(e, t, i)) : (B && s && Ti(t), t.flags |= 1, $s(e, t, r, i), t.child);
	}
	function tc(e, t, n, r, i) {
		if (e === null) {
			var a = n.type;
			return typeof a == "function" && !si(a) && a.defaultProps === void 0 && n.compare === null ? (t.tag = 15, t.type = a, nc(e, t, a, r, i)) : (e = ui(n.type, null, r, t, t.mode, i), e.ref = t.ref, e.return = t, t.child = e);
		}
		if (a = e.child, !W(e, i)) {
			var o = a.memoizedProps;
			if (n = n.compare, n = n === null ? br : n, n(o, r) && e.ref === t.ref) return bc(e, t, i);
		}
		return t.flags |= 1, e = ci(a, r), e.ref = t.ref, e.return = t, t.child = e;
	}
	function nc(e, t, n, r, i) {
		if (e !== null) {
			var a = e.memoizedProps;
			if (br(a, r) && e.ref === t.ref) {
				if (Qs = !1, t.pendingProps = r = a, W(e, i)) e.flags & 131072 && (Qs = !0);
				else return t.lanes = e.lanes, bc(e, t, i);
			}
		}
		return oc(e, t, n, r, i);
	}
	function rc(e, t, n) {
		var r = t.pendingProps, i = r.children, a = e === null ? null : e.memoizedState;
		if (r.mode === "hidden") {
			if (t.flags & 128) {
				if (r = a === null ? n : a.baseLanes | n, e !== null) {
					for (i = t.child = e.child, a = 0; i !== null;) a = a | i.lanes | i.childLanes, i = i.sibling;
					t.childLanes = a & ~r;
				} else t.childLanes = 0, t.child = null;
				return ic(e, t, r, n);
			}
			if (n & 536870912) t.memoizedState = {
				baseLanes: 0,
				cachePool: null
			}, e !== null && ma(t, a === null ? null : a.cachePool), a === null ? Ba() : za(t, a), js(t);
			else return t.lanes = t.childLanes = 536870912, ic(e, t, a === null ? n : a.baseLanes | n, n);
		} else a === null ? (e !== null && ma(t, null), Ba(), Ms(t)) : (ma(t, a.cachePool), za(t, a), Ms(t), t.memoizedState = null);
		return $s(e, t, i, n), t.child;
	}
	function ic(e, t, n, r) {
		var i = pa();
		return i = i === null ? null : {
			parent: ta._currentValue,
			pool: i
		}, t.memoizedState = {
			baseLanes: n,
			cachePool: i
		}, e !== null && ma(t, null), Ba(), js(t), e !== null && Ki(e, t, r, !0), null;
	}
	function ac(e, t) {
		var n = t.ref;
		if (n === null) e !== null && e.ref !== null && (t.flags |= 4194816);
		else {
			if (typeof n != "function" && typeof n != "object") throw Error(i(284));
			(e === null || e.ref !== n) && (t.flags |= 4194816);
		}
	}
	function oc(e, t, n, r, i) {
		return Ji(t), n = $a(e, t, n, r, void 0, i), r = ro(), e !== null && !Qs ? (io(e, t, i), bc(e, t, i)) : (B && r && Ti(t), t.flags |= 1, $s(e, t, n, i), t.child);
	}
	function sc(e, t, n, r, i, a) {
		return Ji(t), t.updateQueue = null, n = to(t, r, n, i), eo(e), r = ro(), e !== null && !Qs ? (io(e, t, a), bc(e, t, a)) : (B && r && Ti(t), t.flags |= 1, $s(e, t, n, a), t.child);
	}
	function cc(e, t, n, r, i) {
		if (Ji(t), t.stateNode === null) {
			var a = ii, o = n.contextType;
			typeof o == "object" && o && (a = Yi(o)), a = new n(r, a), t.memoizedState = a.state !== null && a.state !== void 0 ? a.state : null, a.updater = Ls, t.stateNode = a, a._reactInternals = t, a = t.stateNode, a.props = r, a.state = t.memoizedState, a.refs = {}, Da(t), o = n.contextType, a.context = typeof o == "object" && o ? Yi(o) : ii, a.state = t.memoizedState, o = n.getDerivedStateFromProps, typeof o == "function" && (Is(t, n, o, r), a.state = t.memoizedState), typeof n.getDerivedStateFromProps == "function" || typeof a.getSnapshotBeforeUpdate == "function" || typeof a.UNSAFE_componentWillMount != "function" && typeof a.componentWillMount != "function" || (o = a.state, typeof a.componentWillMount == "function" && a.componentWillMount(), typeof a.UNSAFE_componentWillMount == "function" && a.UNSAFE_componentWillMount(), o !== a.state && Ls.enqueueReplaceState(a, a.state, null), Pa(t, r, a, i), Na(), a.state = t.memoizedState), typeof a.componentDidMount == "function" && (t.flags |= 4194308), r = !0;
		} else if (e === null) {
			a = t.stateNode;
			var s = t.memoizedProps, c = Bs(n, s);
			a.props = c;
			var l = a.context, u = n.contextType;
			o = ii, typeof u == "object" && u && (o = Yi(u));
			var d = n.getDerivedStateFromProps;
			u = typeof d == "function" || typeof a.getSnapshotBeforeUpdate == "function", s = t.pendingProps !== s, u || typeof a.UNSAFE_componentWillReceiveProps != "function" && typeof a.componentWillReceiveProps != "function" || (s || l !== o) && zs(t, a, r, o), Ea = !1;
			var f = t.memoizedState;
			a.state = f, Pa(t, r, a, i), Na(), l = t.memoizedState, s || f !== l || Ea ? (typeof d == "function" && (Is(t, n, d, r), l = t.memoizedState), (c = Ea || Rs(t, n, c, r, f, l, o)) ? (u || typeof a.UNSAFE_componentWillMount != "function" && typeof a.componentWillMount != "function" || (typeof a.componentWillMount == "function" && a.componentWillMount(), typeof a.UNSAFE_componentWillMount == "function" && a.UNSAFE_componentWillMount()), typeof a.componentDidMount == "function" && (t.flags |= 4194308)) : (typeof a.componentDidMount == "function" && (t.flags |= 4194308), t.memoizedProps = r, t.memoizedState = l), a.props = r, a.state = l, a.context = o, r = c) : (typeof a.componentDidMount == "function" && (t.flags |= 4194308), r = !1);
		} else {
			a = t.stateNode, Oa(e, t), o = t.memoizedProps, u = Bs(n, o), a.props = u, d = t.pendingProps, f = a.context, l = n.contextType, c = ii, typeof l == "object" && l && (c = Yi(l)), s = n.getDerivedStateFromProps, (l = typeof s == "function" || typeof a.getSnapshotBeforeUpdate == "function") || typeof a.UNSAFE_componentWillReceiveProps != "function" && typeof a.componentWillReceiveProps != "function" || (o !== d || f !== c) && zs(t, a, r, c), Ea = !1, f = t.memoizedState, a.state = f, Pa(t, r, a, i), Na();
			var p = t.memoizedState;
			o !== d || f !== p || Ea || e !== null && e.dependencies !== null && qi(e.dependencies) ? (typeof s == "function" && (Is(t, n, s, r), p = t.memoizedState), (u = Ea || Rs(t, n, u, r, f, p, c) || e !== null && e.dependencies !== null && qi(e.dependencies)) ? (l || typeof a.UNSAFE_componentWillUpdate != "function" && typeof a.componentWillUpdate != "function" || (typeof a.componentWillUpdate == "function" && a.componentWillUpdate(r, p, c), typeof a.UNSAFE_componentWillUpdate == "function" && a.UNSAFE_componentWillUpdate(r, p, c)), typeof a.componentDidUpdate == "function" && (t.flags |= 4), typeof a.getSnapshotBeforeUpdate == "function" && (t.flags |= 1024)) : (typeof a.componentDidUpdate != "function" || o === e.memoizedProps && f === e.memoizedState || (t.flags |= 4), typeof a.getSnapshotBeforeUpdate != "function" || o === e.memoizedProps && f === e.memoizedState || (t.flags |= 1024), t.memoizedProps = r, t.memoizedState = p), a.props = r, a.state = p, a.context = c, r = u) : (typeof a.componentDidUpdate != "function" || o === e.memoizedProps && f === e.memoizedState || (t.flags |= 4), typeof a.getSnapshotBeforeUpdate != "function" || o === e.memoizedProps && f === e.memoizedState || (t.flags |= 1024), r = !1);
		}
		return a = r, ac(e, t), r = !!(t.flags & 128), a || r ? (a = t.stateNode, n = r && typeof n.getDerivedStateFromError != "function" ? null : a.render(), t.flags |= 1, e !== null && r ? (t.child = Es(t, e.child, null, i), t.child = Es(t, null, n, i)) : $s(e, t, n, i), t.memoizedState = a.state, e = t.child) : e = bc(e, t, i), e;
	}
	function lc(e, t, n, r) {
		return Ii(), t.flags |= 256, $s(e, t, n, r), t.child;
	}
	var uc = {
		dehydrated: null,
		treeContext: null,
		retryLane: 0,
		hydrationErrors: null
	};
	function dc(e) {
		return {
			baseLanes: e,
			cachePool: ha()
		};
	}
	function fc(e, t, n) {
		return e = e === null ? 0 : e.childLanes & ~n, t && (e |= Ll), e;
	}
	function pc(e, t, n) {
		var r = t.pendingProps, a = !1, o = !!(t.flags & 128), s;
		if ((s = o) || (s = e !== null && e.memoizedState === null ? !1 : !!(Ps.current & 2)), s && (a = !0, t.flags &= -129), s = !!(t.flags & 32), t.flags &= -33, e === null) {
			if (B) {
				if (a ? As(t) : Ms(t), B) {
					var c = Oi, l;
					if (l = c) {
						c: {
							for (l = c, c = Ai; l.nodeType !== 8;) if (!c || (l = Wd(l.nextSibling), l === null)) {
								c = null;
								break c;
							}
							c = l;
						}
						c === null ? l = !1 : (t.memoizedState = {
							dehydrated: c,
							treeContext: bi === null ? null : {
								id: xi,
								overflow: Si
							},
							retryLane: 536870912,
							hydrationErrors: null
						}, l = oi(18, null, null, 0), l.stateNode = c, l.return = t, t.child = l, Di = t, Oi = null, l = !0);
					}
					l || Mi(t);
				}
				if (c = t.memoizedState, c !== null && (c = c.dehydrated, c !== null)) return Hd(c) ? t.lanes = 32 : t.lanes = 536870912, null;
				Ns(t);
			}
			return c = r.children, r = r.fallback, a ? (Ms(t), a = t.mode, c = hc({
				mode: "hidden",
				children: c
			}, a), r = di(r, a, n, null), c.return = t, r.return = t, c.sibling = r, t.child = c, a = t.child, a.memoizedState = dc(n), a.childLanes = fc(e, s, n), t.memoizedState = uc, r) : (As(t), mc(t, c));
		}
		if (l = e.memoizedState, l !== null && (c = l.dehydrated, c !== null)) {
			if (o) t.flags & 256 ? (As(t), t.flags &= -257, t = gc(e, t, n)) : t.memoizedState === null ? (Ms(t), a = r.fallback, c = t.mode, r = hc({
				mode: "visible",
				children: r.children
			}, c), a = di(a, c, n, null), a.flags |= 2, r.return = t, a.return = t, r.sibling = a, t.child = r, Es(t, e.child, null, n), r = t.child, r.memoizedState = dc(n), r.childLanes = fc(e, s, n), t.memoizedState = uc, t = a) : (Ms(t), t.child = e.child, t.flags |= 128, t = null);
			else if (As(t), Hd(c)) {
				if (s = c.nextSibling && c.nextSibling.dataset, s) var u = s.dgst;
				s = u, r = Error(i(419)), r.stack = "", r.digest = s, Ri({
					value: r,
					source: null,
					stack: null
				}), t = gc(e, t, n);
			} else if (Qs || Ki(e, t, n, !1), s = (n & e.childLanes) !== 0, Qs || s) {
				if (s = q, s !== null && (r = n & -n, r = r & 42 ? 1 : Ke(r), r = (r & (s.suspendedLanes | n)) === 0 ? r : 0, r !== 0 && r !== l.retryLane)) throw l.retryLane = r, ti(e, r), ru(s, e, r), Zs;
				c.data === "$?" || mu(), t = gc(e, t, n);
			} else c.data === "$?" ? (t.flags |= 192, t.child = e.child, t = null) : (e = l.treeContext, Oi = Wd(c.nextSibling), Di = t, B = !0, ki = null, Ai = !1, e !== null && (vi[yi++] = xi, vi[yi++] = Si, vi[yi++] = bi, xi = e.id, Si = e.overflow, bi = t), t = mc(t, r.children), t.flags |= 4096);
			return t;
		}
		return a ? (Ms(t), a = r.fallback, c = t.mode, l = e.child, u = l.sibling, r = ci(l, {
			mode: "hidden",
			children: r.children
		}), r.subtreeFlags = l.subtreeFlags & 65011712, u === null ? (a = di(a, c, n, null), a.flags |= 2) : a = ci(u, a), a.return = t, r.return = t, r.sibling = a, t.child = r, r = a, a = t.child, c = e.child.memoizedState, c === null ? c = dc(n) : (l = c.cachePool, l === null ? l = ha() : (u = ta._currentValue, l = l.parent === u ? l : {
			parent: u,
			pool: u
		}), c = {
			baseLanes: c.baseLanes | n,
			cachePool: l
		}), a.memoizedState = c, a.childLanes = fc(e, s, n), t.memoizedState = uc, r) : (As(t), n = e.child, e = n.sibling, n = ci(n, {
			mode: "visible",
			children: r.children
		}), n.return = t, n.sibling = null, e !== null && (s = t.deletions, s === null ? (t.deletions = [e], t.flags |= 16) : s.push(e)), t.child = n, t.memoizedState = null, n);
	}
	function mc(e, t) {
		return t = hc({
			mode: "visible",
			children: t
		}, e.mode), t.return = e, e.child = t;
	}
	function hc(e, t) {
		return e = oi(22, e, null, t), e.lanes = 0, e.stateNode = {
			_visibility: 1,
			_pendingMarkers: null,
			_retryCache: null,
			_transitions: null
		}, e;
	}
	function gc(e, t, n) {
		return Es(t, e.child, null, n), e = mc(t, t.pendingProps.children), e.flags |= 2, t.memoizedState = null, e;
	}
	function _c(e, t, n) {
		e.lanes |= t;
		var r = e.alternate;
		r !== null && (r.lanes |= t), Wi(e.return, t, n);
	}
	function vc(e, t, n, r, i) {
		var a = e.memoizedState;
		a === null ? e.memoizedState = {
			isBackwards: t,
			rendering: null,
			renderingStartTime: 0,
			last: r,
			tail: n,
			tailMode: i
		} : (a.isBackwards = t, a.rendering = null, a.renderingStartTime = 0, a.last = r, a.tail = n, a.tailMode = i);
	}
	function yc(e, t, n) {
		var r = t.pendingProps, i = r.revealOrder, a = r.tail;
		if ($s(e, t, r.children, n), r = Ps.current, r & 2) r = r & 1 | 2, t.flags |= 128;
		else {
			if (e !== null && e.flags & 128) a: for (e = t.child; e !== null;) {
				if (e.tag === 13) e.memoizedState !== null && _c(e, n, t);
				else if (e.tag === 19) _c(e, n, t);
				else if (e.child !== null) {
					e.child.return = e, e = e.child;
					continue;
				}
				if (e === t) break a;
				for (; e.sibling === null;) {
					if (e.return === null || e.return === t) break a;
					e = e.return;
				}
				e.sibling.return = e.return, e = e.sibling;
			}
			r &= 1;
		}
		switch (z(Ps, r), i) {
			case "forwards":
				for (n = t.child, i = null; n !== null;) e = n.alternate, e !== null && Fs(e) === null && (i = n), n = n.sibling;
				n = i, n === null ? (i = t.child, t.child = null) : (i = n.sibling, n.sibling = null), vc(t, !1, i, n, a);
				break;
			case "backwards":
				for (n = null, i = t.child, t.child = null; i !== null;) {
					if (e = i.alternate, e !== null && Fs(e) === null) {
						t.child = i;
						break;
					}
					e = i.sibling, i.sibling = n, n = i, i = e;
				}
				vc(t, !0, n, null, a);
				break;
			case "together":
				vc(t, !1, null, null, void 0);
				break;
			default: t.memoizedState = null;
		}
		return t.child;
	}
	function bc(e, t, n) {
		if (e !== null && (t.dependencies = e.dependencies), Pl |= t.lanes, (n & t.childLanes) === 0) {
			if (e !== null) {
				if (Ki(e, t, n, !1), (n & t.childLanes) === 0) return null;
			} else return null;
		}
		if (e !== null && t.child !== e.child) throw Error(i(153));
		if (t.child !== null) {
			for (e = t.child, n = ci(e, e.pendingProps), t.child = n, n.return = t; e.sibling !== null;) e = e.sibling, n = n.sibling = ci(e, e.pendingProps), n.return = t;
			n.sibling = null;
		}
		return t.child;
	}
	function W(e, t) {
		return (e.lanes & t) !== 0 || (e = e.dependencies, !!(e !== null && qi(e)));
	}
	function G(e, t, n) {
		switch (t.tag) {
			case 3:
				ce(t, t.stateNode.containerInfo), Hi(t, ta, e.memoizedState.cache), Ii();
				break;
			case 27:
			case 5:
				ue(t);
				break;
			case 4:
				ce(t, t.stateNode.containerInfo);
				break;
			case 10:
				Hi(t, t.type, t.memoizedProps.value);
				break;
			case 13:
				var r = t.memoizedState;
				if (r !== null) return r.dehydrated === null ? (n & t.child.childLanes) === 0 ? (As(t), e = bc(e, t, n), e === null ? null : e.sibling) : pc(e, t, n) : (As(t), t.flags |= 128, null);
				As(t);
				break;
			case 19:
				var i = !!(e.flags & 128);
				if (r = (n & t.childLanes) !== 0, r ||= (Ki(e, t, n, !1), (n & t.childLanes) !== 0), i) {
					if (r) return yc(e, t, n);
					t.flags |= 128;
				}
				if (i = t.memoizedState, i !== null && (i.rendering = null, i.tail = null, i.lastEffect = null), z(Ps, Ps.current), r) break;
				return null;
			case 22:
			case 23: return t.lanes = 0, rc(e, t, n);
			case 24: Hi(t, ta, e.memoizedState.cache);
		}
		return bc(e, t, n);
	}
	function xc(e, t, n) {
		if (e !== null) {
			if (e.memoizedProps !== t.pendingProps) Qs = !0;
			else {
				if (!W(e, n) && !(t.flags & 128)) return Qs = !1, G(e, t, n);
				Qs = !!(e.flags & 131072);
			}
		} else Qs = !1, B && t.flags & 1048576 && wi(t, _i, t.index);
		switch (t.lanes = 0, t.tag) {
			case 16:
				a: {
					e = t.pendingProps;
					var r = t.elementType, a = r._init;
					if (r = a(r._payload), t.type = r, typeof r == "function") si(r) ? (e = Bs(r, e), t.tag = 1, t = cc(null, t, r, e, n)) : (t.tag = 0, t = oc(null, t, r, e, n));
					else {
						if (r != null) {
							if (a = r.$$typeof, a === w) {
								t.tag = 11, t = ec(null, t, r, e, n);
								break a;
							}
							if (a === D) {
								t.tag = 14, t = tc(null, t, r, e, n);
								break a;
							}
						}
						throw t = N(r) || r, Error(i(306, t, ""));
					}
				}
				return t;
			case 0: return oc(e, t, t.type, t.pendingProps, n);
			case 1: return r = t.type, a = Bs(r, t.pendingProps), cc(e, t, r, a, n);
			case 3:
				a: {
					if (ce(t, t.stateNode.containerInfo), e === null) throw Error(i(387));
					r = t.pendingProps;
					var o = t.memoizedState;
					a = o.element, Oa(e, t), Pa(t, r, null, n);
					var s = t.memoizedState;
					if (r = s.cache, Hi(t, ta, r), r !== o.cache && Gi(t, [ta], n, !0), Na(), r = s.element, o.isDehydrated) {
						if (o = {
							element: r,
							isDehydrated: !1,
							cache: s.cache
						}, t.updateQueue.baseState = o, t.memoizedState = o, t.flags & 256) {
							t = lc(e, t, r, n);
							break a;
						}
						if (r !== a) {
							a = Jr(Error(i(424)), t), Ri(a), t = lc(e, t, r, n);
							break a;
						}
						switch (e = t.stateNode.containerInfo, e.nodeType) {
							case 9:
								e = e.body;
								break;
							default: e = e.nodeName === "HTML" ? e.ownerDocument.body : e;
						}
						for (Oi = Wd(e.firstChild), Di = t, B = !0, ki = null, Ai = !0, n = Ds(t, null, r, n), t.child = n; n;) n.flags = n.flags & -3 | 4096, n = n.sibling;
					} else {
						if (Ii(), r === a) {
							t = bc(e, t, n);
							break a;
						}
						$s(e, t, r, n);
					}
					t = t.child;
				}
				return t;
			case 26: return ac(e, t), e === null ? (n = df(t.type, null, t.pendingProps, null)) ? t.memoizedState = n : B || (n = t.type, e = t.pendingProps, r = Ed(oe.current).createElement(n), r[Ze] = t, r[Qe] = e, Sd(r, n, e), ut(r), t.stateNode = r) : t.memoizedState = df(t.type, e.memoizedProps, t.pendingProps, e.memoizedState), null;
			case 27: return ue(t), e === null && B && (r = t.stateNode = qd(t.type, t.pendingProps, oe.current), Di = t, Ai = !0, a = Oi, Ld(t.type) ? (Gd = a, Oi = Wd(r.firstChild)) : Oi = a), $s(e, t, t.pendingProps.children, n), ac(e, t), e === null && (t.flags |= 4194304), t.child;
			case 5: return e === null && B && ((a = r = Oi) && (r = Bd(r, t.type, t.pendingProps, Ai), r === null ? a = !1 : (t.stateNode = r, Di = t, Oi = Wd(r.firstChild), Ai = !1, a = !0)), a || Mi(t)), ue(t), a = t.type, o = t.pendingProps, s = e === null ? null : e.memoizedProps, r = o.children, kd(a, o) ? r = null : s !== null && kd(a, s) && (t.flags |= 32), t.memoizedState !== null && (a = $a(e, t, no, null, null, n), Ff._currentValue = a), ac(e, t), $s(e, t, r, n), t.child;
			case 6: return e === null && B && ((e = n = Oi) && (n = Vd(n, t.pendingProps, Ai), n === null ? e = !1 : (t.stateNode = n, Di = t, Oi = null, e = !0)), e || Mi(t)), null;
			case 13: return pc(e, t, n);
			case 4: return ce(t, t.stateNode.containerInfo), r = t.pendingProps, e === null ? t.child = Es(t, null, r, n) : $s(e, t, r, n), t.child;
			case 11: return ec(e, t, t.type, t.pendingProps, n);
			case 7: return $s(e, t, t.pendingProps, n), t.child;
			case 8: return $s(e, t, t.pendingProps.children, n), t.child;
			case 12: return $s(e, t, t.pendingProps.children, n), t.child;
			case 10: return r = t.pendingProps, Hi(t, t.type, r.value), $s(e, t, r.children, n), t.child;
			case 9: return a = t.type._context, r = t.pendingProps.children, Ji(t), a = Yi(a), r = r(a), t.flags |= 1, $s(e, t, r, n), t.child;
			case 14: return tc(e, t, t.type, t.pendingProps, n);
			case 15: return nc(e, t, t.type, t.pendingProps, n);
			case 19: return yc(e, t, n);
			case 31: return r = t.pendingProps, n = t.mode, r = {
				mode: r.mode,
				children: r.children
			}, e === null ? (n = hc(r, n), n.ref = t.ref, t.child = n, n.return = t, t = n) : (n = ci(e.child, r), n.ref = t.ref, t.child = n, n.return = t, t = n), t;
			case 22: return rc(e, t, n);
			case 24: return Ji(t), r = Yi(ta), e === null ? (a = pa(), a === null && (a = q, o = na(), a.pooledCache = o, o.refCount++, o !== null && (a.pooledCacheLanes |= n), a = o), t.memoizedState = {
				parent: r,
				cache: a
			}, Da(t), Hi(t, ta, a)) : ((e.lanes & n) !== 0 && (Oa(e, t), Pa(t, null, null, n), Na()), a = e.memoizedState, o = t.memoizedState, a.parent === r ? (r = o.cache, Hi(t, ta, r), r !== a.cache && Gi(t, [ta], n, !0)) : (a = {
				parent: r,
				cache: r
			}, t.memoizedState = a, t.lanes === 0 && (t.memoizedState = t.updateQueue.baseState = a), Hi(t, ta, r))), $s(e, t, t.pendingProps.children, n), t.child;
			case 29: throw t.pendingProps;
		}
		throw Error(i(156, t.tag));
	}
	function Sc(e) {
		e.flags |= 4;
	}
	function Cc(e, t) {
		if (t.type !== "stylesheet" || t.state.loading & 4) e.flags &= -16777217;
		else if (e.flags |= 16777216, !Ef(t)) {
			if (t = Os.current, t !== null && ((Y & 4194048) === Y ? ks !== null : (Y & 62914560) !== Y && !(Y & 536870912) || t !== ks)) throw Ca = ya, _a;
			e.flags |= 8192;
		}
	}
	function wc(e, t) {
		t !== null && (e.flags |= 4), e.flags & 16384 && (t = e.tag === 22 ? 536870912 : Be(), e.lanes |= t, Rl |= t);
	}
	function Tc(e, t) {
		if (!B) switch (e.tailMode) {
			case "hidden":
				t = e.tail;
				for (var n = null; t !== null;) t.alternate !== null && (n = t), t = t.sibling;
				n === null ? e.tail = null : n.sibling = null;
				break;
			case "collapsed":
				n = e.tail;
				for (var r = null; n !== null;) n.alternate !== null && (r = n), n = n.sibling;
				r === null ? t || e.tail === null ? e.tail = null : e.tail.sibling = null : r.sibling = null;
		}
	}
	function Ec(e) {
		var t = e.alternate !== null && e.alternate.child === e.child, n = 0, r = 0;
		if (t) for (var i = e.child; i !== null;) n |= i.lanes | i.childLanes, r |= i.subtreeFlags & 65011712, r |= i.flags & 65011712, i.return = e, i = i.sibling;
		else for (i = e.child; i !== null;) n |= i.lanes | i.childLanes, r |= i.subtreeFlags, r |= i.flags, i.return = e, i = i.sibling;
		return e.subtreeFlags |= r, e.childLanes = n, t;
	}
	function Dc(e, t, n) {
		var r = t.pendingProps;
		switch (Ei(t), t.tag) {
			case 31:
			case 16:
			case 15:
			case 0:
			case 11:
			case 7:
			case 8:
			case 12:
			case 9:
			case 14: return Ec(t), null;
			case 1: return Ec(t), null;
			case 3: return n = t.stateNode, r = null, e !== null && (r = e.memoizedState.cache), t.memoizedState.cache !== r && (t.flags |= 2048), Ui(ta), le(), n.pendingContext && (n.context = n.pendingContext, n.pendingContext = null), (e === null || e.child === null) && (Fi(t) ? Sc(t) : e === null || e.memoizedState.isDehydrated && !(t.flags & 256) || (t.flags |= 1024, Li())), Ec(t), null;
			case 26: return n = t.memoizedState, e === null ? (Sc(t), n === null ? (Ec(t), t.flags &= -16777217) : (Ec(t), Cc(t, n))) : n ? n === e.memoizedState ? (Ec(t), t.flags &= -16777217) : (Sc(t), Ec(t), Cc(t, n)) : (e.memoizedProps !== r && Sc(t), Ec(t), t.flags &= -16777217), null;
			case 27:
				de(t), n = oe.current;
				var a = t.type;
				if (e !== null && t.stateNode != null) e.memoizedProps !== r && Sc(t);
				else {
					if (!r) {
						if (t.stateNode === null) throw Error(i(166));
						return Ec(t), null;
					}
					e = ie.current, Fi(t) ? Ni(t, e) : (e = qd(a, r, n), t.stateNode = e, Sc(t));
				}
				return Ec(t), null;
			case 5:
				if (de(t), n = t.type, e !== null && t.stateNode != null) e.memoizedProps !== r && Sc(t);
				else {
					if (!r) {
						if (t.stateNode === null) throw Error(i(166));
						return Ec(t), null;
					}
					if (e = ie.current, Fi(t)) Ni(t, e);
					else {
						switch (a = Ed(oe.current), e) {
							case 1:
								e = a.createElementNS("http://www.w3.org/2000/svg", n);
								break;
							case 2:
								e = a.createElementNS("http://www.w3.org/1998/Math/MathML", n);
								break;
							default: switch (n) {
								case "svg":
									e = a.createElementNS("http://www.w3.org/2000/svg", n);
									break;
								case "math":
									e = a.createElementNS("http://www.w3.org/1998/Math/MathML", n);
									break;
								case "script":
									e = a.createElement("div"), e.innerHTML = "<script><\/script>", e = e.removeChild(e.firstChild);
									break;
								case "select":
									e = typeof r.is == "string" ? a.createElement("select", { is: r.is }) : a.createElement("select"), r.multiple ? e.multiple = !0 : r.size && (e.size = r.size);
									break;
								default: e = typeof r.is == "string" ? a.createElement(n, { is: r.is }) : a.createElement(n);
							}
						}
						e[Ze] = t, e[Qe] = r;
						a: for (a = t.child; a !== null;) {
							if (a.tag === 5 || a.tag === 6) e.appendChild(a.stateNode);
							else if (a.tag !== 4 && a.tag !== 27 && a.child !== null) {
								a.child.return = a, a = a.child;
								continue;
							}
							if (a === t) break a;
							for (; a.sibling === null;) {
								if (a.return === null || a.return === t) break a;
								a = a.return;
							}
							a.sibling.return = a.return, a = a.sibling;
						}
						t.stateNode = e;
						a: switch (Sd(e, n, r), n) {
							case "button":
							case "input":
							case "select":
							case "textarea":
								e = !!r.autoFocus;
								break a;
							case "img":
								e = !0;
								break a;
							default: e = !1;
						}
						e && Sc(t);
					}
				}
				return Ec(t), t.flags &= -16777217, null;
			case 6:
				if (e && t.stateNode != null) e.memoizedProps !== r && Sc(t);
				else {
					if (typeof r != "string" && t.stateNode === null) throw Error(i(166));
					if (e = oe.current, Fi(t)) {
						if (e = t.stateNode, n = t.memoizedProps, r = null, a = Di, a !== null) switch (a.tag) {
							case 27:
							case 5: r = a.memoizedProps;
						}
						e[Ze] = t, e = !!(e.nodeValue === n || r !== null && !0 === r.suppressHydrationWarning || yd(e.nodeValue, n)), e || Mi(t);
					} else e = Ed(e).createTextNode(r), e[Ze] = t, t.stateNode = e;
				}
				return Ec(t), null;
			case 13:
				if (r = t.memoizedState, e === null || e.memoizedState !== null && e.memoizedState.dehydrated !== null) {
					if (a = Fi(t), r !== null && r.dehydrated !== null) {
						if (e === null) {
							if (!a) throw Error(i(318));
							if (a = t.memoizedState, a = a === null ? null : a.dehydrated, !a) throw Error(i(317));
							a[Ze] = t;
						} else Ii(), !(t.flags & 128) && (t.memoizedState = null), t.flags |= 4;
						Ec(t), a = !1;
					} else a = Li(), e !== null && e.memoizedState !== null && (e.memoizedState.hydrationErrors = a), a = !0;
					if (!a) return t.flags & 256 ? (Ns(t), t) : (Ns(t), null);
				}
				if (Ns(t), t.flags & 128) return t.lanes = n, t;
				if (n = r !== null, e = e !== null && e.memoizedState !== null, n) {
					r = t.child, a = null, r.alternate !== null && r.alternate.memoizedState !== null && r.alternate.memoizedState.cachePool !== null && (a = r.alternate.memoizedState.cachePool.pool);
					var o = null;
					r.memoizedState !== null && r.memoizedState.cachePool !== null && (o = r.memoizedState.cachePool.pool), o !== a && (r.flags |= 2048);
				}
				return n !== e && n && (t.child.flags |= 8192), wc(t, t.updateQueue), Ec(t), null;
			case 4: return le(), e === null && ld(t.stateNode.containerInfo), Ec(t), null;
			case 10: return Ui(t.type), Ec(t), null;
			case 19:
				if (R(Ps), a = t.memoizedState, a === null) return Ec(t), null;
				if (r = !!(t.flags & 128), o = a.rendering, o === null) {
					if (r) Tc(a, !1);
					else {
						if (Nl !== 0 || e !== null && e.flags & 128) for (e = t.child; e !== null;) {
							if (o = Fs(e), o !== null) {
								for (t.flags |= 128, Tc(a, !1), e = o.updateQueue, t.updateQueue = e, wc(t, e), t.subtreeFlags = 0, e = n, n = t.child; n !== null;) li(n, e), n = n.sibling;
								return z(Ps, Ps.current & 1 | 2), t.child;
							}
							e = e.sibling;
						}
						a.tail !== null && _e() > Ul && (t.flags |= 128, r = !0, Tc(a, !1), t.lanes = 4194304);
					}
				} else {
					if (!r) {
						if (e = Fs(o), e !== null) {
							if (t.flags |= 128, r = !0, e = e.updateQueue, t.updateQueue = e, wc(t, e), Tc(a, !0), a.tail === null && a.tailMode === "hidden" && !o.alternate && !B) return Ec(t), null;
						} else 2 * _e() - a.renderingStartTime > Ul && n !== 536870912 && (t.flags |= 128, r = !0, Tc(a, !1), t.lanes = 4194304);
					}
					a.isBackwards ? (o.sibling = t.child, t.child = o) : (e = a.last, e === null ? t.child = o : e.sibling = o, a.last = o);
				}
				return a.tail === null ? (Ec(t), null) : (t = a.tail, a.rendering = t, a.tail = t.sibling, a.renderingStartTime = _e(), t.sibling = null, e = Ps.current, z(Ps, r ? e & 1 | 2 : e & 1), t);
			case 22:
			case 23: return Ns(t), Va(), r = t.memoizedState !== null, e === null ? r && (t.flags |= 8192) : e.memoizedState !== null !== r && (t.flags |= 8192), r ? n & 536870912 && !(t.flags & 128) && (Ec(t), t.subtreeFlags & 6 && (t.flags |= 8192)) : Ec(t), n = t.updateQueue, n !== null && wc(t, n.retryQueue), n = null, e !== null && e.memoizedState !== null && e.memoizedState.cachePool !== null && (n = e.memoizedState.cachePool.pool), r = null, t.memoizedState !== null && t.memoizedState.cachePool !== null && (r = t.memoizedState.cachePool.pool), r !== n && (t.flags |= 2048), e !== null && R(fa), null;
			case 24: return n = null, e !== null && (n = e.memoizedState.cache), t.memoizedState.cache !== n && (t.flags |= 2048), Ui(ta), Ec(t), null;
			case 25: return null;
			case 30: return null;
		}
		throw Error(i(156, t.tag));
	}
	function Oc(e, t) {
		switch (Ei(t), t.tag) {
			case 1: return e = t.flags, e & 65536 ? (t.flags = e & -65537 | 128, t) : null;
			case 3: return Ui(ta), le(), e = t.flags, e & 65536 && !(e & 128) ? (t.flags = e & -65537 | 128, t) : null;
			case 26:
			case 27:
			case 5: return de(t), null;
			case 13:
				if (Ns(t), e = t.memoizedState, e !== null && e.dehydrated !== null) {
					if (t.alternate === null) throw Error(i(340));
					Ii();
				}
				return e = t.flags, e & 65536 ? (t.flags = e & -65537 | 128, t) : null;
			case 19: return R(Ps), null;
			case 4: return le(), null;
			case 10: return Ui(t.type), null;
			case 22:
			case 23: return Ns(t), Va(), e !== null && R(fa), e = t.flags, e & 65536 ? (t.flags = e & -65537 | 128, t) : null;
			case 24: return Ui(ta), null;
			case 25: return null;
			default: return null;
		}
	}
	function kc(e, t) {
		switch (Ei(t), t.tag) {
			case 3:
				Ui(ta), le();
				break;
			case 26:
			case 27:
			case 5:
				de(t);
				break;
			case 4:
				le();
				break;
			case 13:
				Ns(t);
				break;
			case 19:
				R(Ps);
				break;
			case 10:
				Ui(t.type);
				break;
			case 22:
			case 23:
				Ns(t), Va(), e !== null && R(fa);
				break;
			case 24: Ui(ta);
		}
	}
	function Ac(e, t) {
		try {
			var n = t.updateQueue, r = n === null ? null : n.lastEffect;
			if (r !== null) {
				var i = r.next;
				n = i;
				do {
					if ((n.tag & e) === e) {
						r = void 0;
						var a = n.create, o = n.inst;
						r = a(), o.destroy = r;
					}
					n = n.next;
				} while (n !== i);
			}
		} catch (e) {
			Z(t, t.return, e);
		}
	}
	function jc(e, t, n) {
		try {
			var r = t.updateQueue, i = r === null ? null : r.lastEffect;
			if (i !== null) {
				var a = i.next;
				r = a;
				do {
					if ((r.tag & e) === e) {
						var o = r.inst, s = o.destroy;
						if (s !== void 0) {
							o.destroy = void 0, i = t;
							var c = n, l = s;
							try {
								l();
							} catch (e) {
								Z(i, c, e);
							}
						}
					}
					r = r.next;
				} while (r !== a);
			}
		} catch (e) {
			Z(t, t.return, e);
		}
	}
	function Mc(e) {
		var t = e.updateQueue;
		if (t !== null) {
			var n = e.stateNode;
			try {
				Ia(t, n);
			} catch (t) {
				Z(e, e.return, t);
			}
		}
	}
	function Nc(e, t, n) {
		n.props = Bs(e.type, e.memoizedProps), n.state = e.memoizedState;
		try {
			n.componentWillUnmount();
		} catch (n) {
			Z(e, t, n);
		}
	}
	function Pc(e, t) {
		try {
			var n = e.ref;
			if (n !== null) {
				switch (e.tag) {
					case 26:
					case 27:
					case 5:
						var r = e.stateNode;
						break;
					case 30:
						r = e.stateNode;
						break;
					default: r = e.stateNode;
				}
				typeof n == "function" ? e.refCleanup = n(r) : n.current = r;
			}
		} catch (n) {
			Z(e, t, n);
		}
	}
	function Fc(e, t) {
		var n = e.ref, r = e.refCleanup;
		if (n !== null) {
			if (typeof r == "function") try {
				r();
			} catch (n) {
				Z(e, t, n);
			} finally {
				e.refCleanup = null, e = e.alternate, e != null && (e.refCleanup = null);
			}
			else if (typeof n == "function") try {
				n(null);
			} catch (n) {
				Z(e, t, n);
			}
			else n.current = null;
		}
	}
	function Ic(e) {
		var t = e.type, n = e.memoizedProps, r = e.stateNode;
		try {
			a: switch (t) {
				case "button":
				case "input":
				case "select":
				case "textarea":
					n.autoFocus && r.focus();
					break a;
				case "img": n.src ? r.src = n.src : n.srcSet && (r.srcset = n.srcSet);
			}
		} catch (t) {
			Z(e, e.return, t);
		}
	}
	function Lc(e, t, n) {
		try {
			var r = e.stateNode;
			Cd(r, e.type, n, t), r[Qe] = t;
		} catch (t) {
			Z(e, e.return, t);
		}
	}
	function Rc(e) {
		return e.tag === 5 || e.tag === 3 || e.tag === 26 || e.tag === 27 && Ld(e.type) || e.tag === 4;
	}
	function zc(e) {
		a: for (;;) {
			for (; e.sibling === null;) {
				if (e.return === null || Rc(e.return)) return null;
				e = e.return;
			}
			for (e.sibling.return = e.return, e = e.sibling; e.tag !== 5 && e.tag !== 6 && e.tag !== 18;) {
				if (e.tag === 27 && Ld(e.type) || e.flags & 2 || e.child === null || e.tag === 4) continue a;
				e.child.return = e, e = e.child;
			}
			if (!(e.flags & 2)) return e.stateNode;
		}
	}
	function Bc(e, t, n) {
		var r = e.tag;
		if (r === 5 || r === 6) e = e.stateNode, t ? (n.nodeType === 9 ? n.body : n.nodeName === "HTML" ? n.ownerDocument.body : n).insertBefore(e, t) : (t = n.nodeType === 9 ? n.body : n.nodeName === "HTML" ? n.ownerDocument.body : n, t.appendChild(e), n = n._reactRootContainer, n != null || t.onclick !== null || (t.onclick = bd));
		else if (r !== 4 && (r === 27 && Ld(e.type) && (n = e.stateNode, t = null), e = e.child, e !== null)) for (Bc(e, t, n), e = e.sibling; e !== null;) Bc(e, t, n), e = e.sibling;
	}
	function Vc(e, t, n) {
		var r = e.tag;
		if (r === 5 || r === 6) e = e.stateNode, t ? n.insertBefore(e, t) : n.appendChild(e);
		else if (r !== 4 && (r === 27 && Ld(e.type) && (n = e.stateNode), e = e.child, e !== null)) for (Vc(e, t, n), e = e.sibling; e !== null;) Vc(e, t, n), e = e.sibling;
	}
	function Hc(e) {
		var t = e.stateNode, n = e.memoizedProps;
		try {
			for (var r = e.type, i = t.attributes; i.length;) t.removeAttributeNode(i[0]);
			Sd(t, r, n), t[Ze] = e, t[Qe] = n;
		} catch (t) {
			Z(e, e.return, t);
		}
	}
	var Uc = !1, Wc = !1, Gc = !1, Kc = typeof WeakSet == "function" ? WeakSet : Set, qc = null;
	function Jc(e, t) {
		if (e = e.containerInfo, wd = Uf, e = wr(e), Tr(e)) {
			if ("selectionStart" in e) var n = {
				start: e.selectionStart,
				end: e.selectionEnd
			};
			else a: {
				n = (n = e.ownerDocument) && n.defaultView || window;
				var r = n.getSelection && n.getSelection();
				if (r && r.rangeCount !== 0) {
					n = r.anchorNode;
					var a = r.anchorOffset, o = r.focusNode;
					r = r.focusOffset;
					try {
						n.nodeType, o.nodeType;
					} catch {
						n = null;
						break a;
					}
					var s = 0, c = -1, l = -1, u = 0, d = 0, f = e, p = null;
					b: for (;;) {
						for (var m; f !== n || a !== 0 && f.nodeType !== 3 || (c = s + a), f !== o || r !== 0 && f.nodeType !== 3 || (l = s + r), f.nodeType === 3 && (s += f.nodeValue.length), (m = f.firstChild) !== null;) p = f, f = m;
						for (;;) {
							if (f === e) break b;
							if (p === n && ++u === a && (c = s), p === o && ++d === r && (l = s), (m = f.nextSibling) !== null) break;
							f = p, p = f.parentNode;
						}
						f = m;
					}
					n = c === -1 || l === -1 ? null : {
						start: c,
						end: l
					};
				} else n = null;
			}
			n ||= {
				start: 0,
				end: 0
			};
		} else n = null;
		for (Td = {
			focusedElem: e,
			selectionRange: n
		}, Uf = !1, qc = t; qc !== null;) if (t = qc, e = t.child, t.subtreeFlags & 1024 && e !== null) e.return = t, qc = e;
		else for (; qc !== null;) {
			switch (t = qc, o = t.alternate, e = t.flags, t.tag) {
				case 0: break;
				case 11:
				case 15: break;
				case 1:
					if (e & 1024 && o !== null) {
						e = void 0, n = t, a = o.memoizedProps, o = o.memoizedState, r = n.stateNode;
						try {
							var h = Bs(n.type, a, n.elementType === n.type);
							e = r.getSnapshotBeforeUpdate(h, o), r.__reactInternalSnapshotBeforeUpdate = e;
						} catch (e) {
							Z(n, n.return, e);
						}
					}
					break;
				case 3:
					if (e & 1024) {
						if (e = t.stateNode.containerInfo, n = e.nodeType, n === 9) zd(e);
						else if (n === 1) switch (e.nodeName) {
							case "HEAD":
							case "HTML":
							case "BODY":
								zd(e);
								break;
							default: e.textContent = "";
						}
					}
					break;
				case 5:
				case 26:
				case 27:
				case 6:
				case 4:
				case 17: break;
				default: if (e & 1024) throw Error(i(163));
			}
			if (e = t.sibling, e !== null) {
				e.return = t.return, qc = e;
				break;
			}
			qc = t.return;
		}
	}
	function Yc(e, t, n) {
		var r = n.flags;
		switch (n.tag) {
			case 0:
			case 11:
			case 15:
				ll(e, n), r & 4 && Ac(5, n);
				break;
			case 1:
				if (ll(e, n), r & 4) {
					if (e = n.stateNode, t === null) try {
						e.componentDidMount();
					} catch (e) {
						Z(n, n.return, e);
					}
					else {
						var i = Bs(n.type, t.memoizedProps);
						t = t.memoizedState;
						try {
							e.componentDidUpdate(i, t, e.__reactInternalSnapshotBeforeUpdate);
						} catch (e) {
							Z(n, n.return, e);
						}
					}
				}
				r & 64 && Mc(n), r & 512 && Pc(n, n.return);
				break;
			case 3:
				if (ll(e, n), r & 64 && (e = n.updateQueue, e !== null)) {
					if (t = null, n.child !== null) switch (n.child.tag) {
						case 27:
						case 5:
							t = n.child.stateNode;
							break;
						case 1: t = n.child.stateNode;
					}
					try {
						Ia(e, t);
					} catch (e) {
						Z(n, n.return, e);
					}
				}
				break;
			case 27: t === null && r & 4 && Hc(n);
			case 26:
			case 5:
				ll(e, n), t === null && r & 4 && Ic(n), r & 512 && Pc(n, n.return);
				break;
			case 12:
				ll(e, n);
				break;
			case 13:
				ll(e, n), r & 4 && tl(e, n), r & 64 && (e = n.memoizedState, e !== null && (e = e.dehydrated, e !== null && (n = Fu.bind(null, n), Ud(e, n))));
				break;
			case 22:
				if (r = n.memoizedState !== null || Uc, !r) {
					t = t !== null && t.memoizedState !== null || Wc, i = Uc;
					var a = Wc;
					Uc = r, (Wc = t) && !a ? dl(e, n, !!(n.subtreeFlags & 8772)) : ll(e, n), Uc = i, Wc = a;
				}
				break;
			case 30: break;
			default: ll(e, n);
		}
	}
	function Xc(e) {
		var t = e.alternate;
		t !== null && (e.alternate = null, Xc(t)), e.child = null, e.deletions = null, e.sibling = null, e.tag === 5 && (t = e.stateNode, t !== null && at(t)), e.stateNode = null, e.return = null, e.dependencies = null, e.memoizedProps = null, e.memoizedState = null, e.pendingProps = null, e.stateNode = null, e.updateQueue = null;
	}
	var Zc = null, Qc = !1;
	function $c(e, t, n) {
		for (n = n.child; n !== null;) el(e, t, n), n = n.sibling;
	}
	function el(e, t, n) {
		if (De && typeof De.onCommitFiberUnmount == "function") try {
			De.onCommitFiberUnmount(Ee, n);
		} catch {}
		switch (n.tag) {
			case 26:
				Wc || Fc(n, t), $c(e, t, n), n.memoizedState ? n.memoizedState.count-- : n.stateNode && (n = n.stateNode, n.parentNode.removeChild(n));
				break;
			case 27:
				Wc || Fc(n, t);
				var r = Zc, i = Qc;
				Ld(n.type) && (Zc = n.stateNode, Qc = !1), $c(e, t, n), Jd(n.stateNode), Zc = r, Qc = i;
				break;
			case 5: Wc || Fc(n, t);
			case 6:
				if (r = Zc, i = Qc, Zc = null, $c(e, t, n), Zc = r, Qc = i, Zc !== null) {
					if (Qc) try {
						(Zc.nodeType === 9 ? Zc.body : Zc.nodeName === "HTML" ? Zc.ownerDocument.body : Zc).removeChild(n.stateNode);
					} catch (e) {
						Z(n, t, e);
					}
					else try {
						Zc.removeChild(n.stateNode);
					} catch (e) {
						Z(n, t, e);
					}
				}
				break;
			case 18:
				Zc !== null && (Qc ? (e = Zc, Rd(e.nodeType === 9 ? e.body : e.nodeName === "HTML" ? e.ownerDocument.body : e, n.stateNode), hp(e)) : Rd(Zc, n.stateNode));
				break;
			case 4:
				r = Zc, i = Qc, Zc = n.stateNode.containerInfo, Qc = !0, $c(e, t, n), Zc = r, Qc = i;
				break;
			case 0:
			case 11:
			case 14:
			case 15:
				Wc || jc(2, n, t), Wc || jc(4, n, t), $c(e, t, n);
				break;
			case 1:
				Wc || (Fc(n, t), r = n.stateNode, typeof r.componentWillUnmount == "function" && Nc(n, t, r)), $c(e, t, n);
				break;
			case 21:
				$c(e, t, n);
				break;
			case 22:
				Wc = (r = Wc) || n.memoizedState !== null, $c(e, t, n), Wc = r;
				break;
			default: $c(e, t, n);
		}
	}
	function tl(e, t) {
		if (t.memoizedState === null && (e = t.alternate, e !== null && (e = e.memoizedState, e !== null && (e = e.dehydrated, e !== null)))) try {
			hp(e);
		} catch (e) {
			Z(t, t.return, e);
		}
	}
	function nl(e) {
		switch (e.tag) {
			case 13:
			case 19:
				var t = e.stateNode;
				return t === null && (t = e.stateNode = new Kc()), t;
			case 22: return e = e.stateNode, t = e._retryCache, t === null && (t = e._retryCache = new Kc()), t;
			default: throw Error(i(435, e.tag));
		}
	}
	function rl(e, t) {
		var n = nl(e);
		t.forEach(function(t) {
			var r = Iu.bind(null, e, t);
			n.has(t) || (n.add(t), t.then(r, r));
		});
	}
	function il(e, t) {
		var n = t.deletions;
		if (n !== null) for (var r = 0; r < n.length; r++) {
			var a = n[r], o = e, s = t, c = s;
			a: for (; c !== null;) {
				switch (c.tag) {
					case 27:
						if (Ld(c.type)) {
							Zc = c.stateNode, Qc = !1;
							break a;
						}
						break;
					case 5:
						Zc = c.stateNode, Qc = !1;
						break a;
					case 3:
					case 4:
						Zc = c.stateNode.containerInfo, Qc = !0;
						break a;
				}
				c = c.return;
			}
			if (Zc === null) throw Error(i(160));
			el(o, s, a), Zc = null, Qc = !1, o = a.alternate, o !== null && (o.return = null), a.return = null;
		}
		if (t.subtreeFlags & 13878) for (t = t.child; t !== null;) ol(t, e), t = t.sibling;
	}
	var al = null;
	function ol(e, t) {
		var n = e.alternate, r = e.flags;
		switch (e.tag) {
			case 0:
			case 11:
			case 14:
			case 15:
				il(t, e), sl(e), r & 4 && (jc(3, e, e.return), Ac(3, e), jc(5, e, e.return));
				break;
			case 1:
				il(t, e), sl(e), r & 512 && (Wc || n === null || Fc(n, n.return)), r & 64 && Uc && (e = e.updateQueue, e !== null && (r = e.callbacks, r !== null && (n = e.shared.hiddenCallbacks, e.shared.hiddenCallbacks = n === null ? r : n.concat(r))));
				break;
			case 26:
				var a = al;
				if (il(t, e), sl(e), r & 512 && (Wc || n === null || Fc(n, n.return)), r & 4) {
					var o = n === null ? null : n.memoizedState;
					if (r = e.memoizedState, n === null) {
						if (r === null) {
							if (e.stateNode === null) {
								a: {
									r = e.type, n = e.memoizedProps, a = a.ownerDocument || a;
									b: switch (r) {
										case "title":
											o = a.getElementsByTagName("title")[0], (!o || o[it] || o[Ze] || o.namespaceURI === "http://www.w3.org/2000/svg" || o.hasAttribute("itemprop")) && (o = a.createElement(r), a.head.insertBefore(o, a.querySelector("head > title"))), Sd(o, r, n), o[Ze] = e, ut(o), r = o;
											break a;
										case "link":
											var s = Cf("link", "href", a).get(r + (n.href || ""));
											if (s) {
												for (var c = 0; c < s.length; c++) if (o = s[c], o.getAttribute("href") === (n.href == null || n.href === "" ? null : n.href) && o.getAttribute("rel") === (n.rel == null ? null : n.rel) && o.getAttribute("title") === (n.title == null ? null : n.title) && o.getAttribute("crossorigin") === (n.crossOrigin == null ? null : n.crossOrigin)) {
													s.splice(c, 1);
													break b;
												}
											}
											o = a.createElement(r), Sd(o, r, n), a.head.appendChild(o);
											break;
										case "meta":
											if (s = Cf("meta", "content", a).get(r + (n.content || ""))) {
												for (c = 0; c < s.length; c++) if (o = s[c], o.getAttribute("content") === (n.content == null ? null : "" + n.content) && o.getAttribute("name") === (n.name == null ? null : n.name) && o.getAttribute("property") === (n.property == null ? null : n.property) && o.getAttribute("http-equiv") === (n.httpEquiv == null ? null : n.httpEquiv) && o.getAttribute("charset") === (n.charSet == null ? null : n.charSet)) {
													s.splice(c, 1);
													break b;
												}
											}
											o = a.createElement(r), Sd(o, r, n), a.head.appendChild(o);
											break;
										default: throw Error(i(468, r));
									}
									o[Ze] = e, ut(o), r = o;
								}
								e.stateNode = r;
							} else wf(a, e.type, e.stateNode);
						} else e.stateNode = vf(a, r, e.memoizedProps);
					} else o === r ? r === null && e.stateNode !== null && Lc(e, e.memoizedProps, n.memoizedProps) : (o === null ? n.stateNode !== null && (n = n.stateNode, n.parentNode.removeChild(n)) : o.count--, r === null ? wf(a, e.type, e.stateNode) : vf(a, r, e.memoizedProps));
				}
				break;
			case 27:
				il(t, e), sl(e), r & 512 && (Wc || n === null || Fc(n, n.return)), n !== null && r & 4 && Lc(e, e.memoizedProps, n.memoizedProps);
				break;
			case 5:
				if (il(t, e), sl(e), r & 512 && (Wc || n === null || Fc(n, n.return)), e.flags & 32) {
					a = e.stateNode;
					try {
						Ut(a, "");
					} catch (t) {
						Z(e, e.return, t);
					}
				}
				r & 4 && e.stateNode != null && (a = e.memoizedProps, Lc(e, a, n === null ? a : n.memoizedProps)), r & 1024 && (Gc = !0);
				break;
			case 6:
				if (il(t, e), sl(e), r & 4) {
					if (e.stateNode === null) throw Error(i(162));
					r = e.memoizedProps, n = e.stateNode;
					try {
						n.nodeValue = r;
					} catch (t) {
						Z(e, e.return, t);
					}
				}
				break;
			case 3:
				if (Sf = null, a = al, al = Zd(t.containerInfo), il(t, e), al = a, sl(e), r & 4 && n !== null && n.memoizedState.isDehydrated) try {
					hp(t.containerInfo);
				} catch (t) {
					Z(e, e.return, t);
				}
				Gc && (Gc = !1, cl(e));
				break;
			case 4:
				r = al, al = Zd(e.stateNode.containerInfo), il(t, e), sl(e), al = r;
				break;
			case 12:
				il(t, e), sl(e);
				break;
			case 13:
				il(t, e), sl(e), e.child.flags & 8192 && e.memoizedState !== null != (n !== null && n.memoizedState !== null) && (Hl = _e()), r & 4 && (r = e.updateQueue, r !== null && (e.updateQueue = null, rl(e, r)));
				break;
			case 22:
				a = e.memoizedState !== null;
				var l = n !== null && n.memoizedState !== null, u = Uc, d = Wc;
				if (Uc = u || a, Wc = d || l, il(t, e), Wc = d, Uc = u, sl(e), r & 8192) a: for (t = e.stateNode, t._visibility = a ? t._visibility & -2 : t._visibility | 1, a && (n === null || l || Uc || Wc || ul(e)), n = null, t = e;;) {
					if (t.tag === 5 || t.tag === 26) {
						if (n === null) {
							l = n = t;
							try {
								if (o = l.stateNode, a) s = o.style, typeof s.setProperty == "function" ? s.setProperty("display", "none", "important") : s.display = "none";
								else {
									c = l.stateNode;
									var f = l.memoizedProps.style, p = f != null && f.hasOwnProperty("display") ? f.display : null;
									c.style.display = p == null || typeof p == "boolean" ? "" : ("" + p).trim();
								}
							} catch (e) {
								Z(l, l.return, e);
							}
						}
					} else if (t.tag === 6) {
						if (n === null) {
							l = t;
							try {
								l.stateNode.nodeValue = a ? "" : l.memoizedProps;
							} catch (e) {
								Z(l, l.return, e);
							}
						}
					} else if ((t.tag !== 22 && t.tag !== 23 || t.memoizedState === null || t === e) && t.child !== null) {
						t.child.return = t, t = t.child;
						continue;
					}
					if (t === e) break a;
					for (; t.sibling === null;) {
						if (t.return === null || t.return === e) break a;
						n === t && (n = null), t = t.return;
					}
					n === t && (n = null), t.sibling.return = t.return, t = t.sibling;
				}
				r & 4 && (r = e.updateQueue, r !== null && (n = r.retryQueue, n !== null && (r.retryQueue = null, rl(e, n))));
				break;
			case 19:
				il(t, e), sl(e), r & 4 && (r = e.updateQueue, r !== null && (e.updateQueue = null, rl(e, r)));
				break;
			case 30: break;
			case 21: break;
			default: il(t, e), sl(e);
		}
	}
	function sl(e) {
		var t = e.flags;
		if (t & 2) {
			try {
				for (var n, r = e.return; r !== null;) {
					if (Rc(r)) {
						n = r;
						break;
					}
					r = r.return;
				}
				if (n == null) throw Error(i(160));
				switch (n.tag) {
					case 27:
						var a = n.stateNode;
						Vc(e, zc(e), a);
						break;
					case 5:
						var o = n.stateNode;
						n.flags & 32 && (Ut(o, ""), n.flags &= -33), Vc(e, zc(e), o);
						break;
					case 3:
					case 4:
						var s = n.stateNode.containerInfo;
						Bc(e, zc(e), s);
						break;
					default: throw Error(i(161));
				}
			} catch (t) {
				Z(e, e.return, t);
			}
			e.flags &= -3;
		}
		t & 4096 && (e.flags &= -4097);
	}
	function cl(e) {
		if (e.subtreeFlags & 1024) for (e = e.child; e !== null;) {
			var t = e;
			cl(t), t.tag === 5 && t.flags & 1024 && t.stateNode.reset(), e = e.sibling;
		}
	}
	function ll(e, t) {
		if (t.subtreeFlags & 8772) for (t = t.child; t !== null;) Yc(e, t.alternate, t), t = t.sibling;
	}
	function ul(e) {
		for (e = e.child; e !== null;) {
			var t = e;
			switch (t.tag) {
				case 0:
				case 11:
				case 14:
				case 15:
					jc(4, t, t.return), ul(t);
					break;
				case 1:
					Fc(t, t.return);
					var n = t.stateNode;
					typeof n.componentWillUnmount == "function" && Nc(t, t.return, n), ul(t);
					break;
				case 27: Jd(t.stateNode);
				case 26:
				case 5:
					Fc(t, t.return), ul(t);
					break;
				case 22:
					t.memoizedState === null && ul(t);
					break;
				case 30:
					ul(t);
					break;
				default: ul(t);
			}
			e = e.sibling;
		}
	}
	function dl(e, t, n) {
		for (n &&= !!(t.subtreeFlags & 8772), t = t.child; t !== null;) {
			var r = t.alternate, i = e, a = t, o = a.flags;
			switch (a.tag) {
				case 0:
				case 11:
				case 15:
					dl(i, a, n), Ac(4, a);
					break;
				case 1:
					if (dl(i, a, n), r = a, i = r.stateNode, typeof i.componentDidMount == "function") try {
						i.componentDidMount();
					} catch (e) {
						Z(r, r.return, e);
					}
					if (r = a, i = r.updateQueue, i !== null) {
						var s = r.stateNode;
						try {
							var c = i.shared.hiddenCallbacks;
							if (c !== null) for (i.shared.hiddenCallbacks = null, i = 0; i < c.length; i++) Fa(c[i], s);
						} catch (e) {
							Z(r, r.return, e);
						}
					}
					n && o & 64 && Mc(a), Pc(a, a.return);
					break;
				case 27: Hc(a);
				case 26:
				case 5:
					dl(i, a, n), n && r === null && o & 4 && Ic(a), Pc(a, a.return);
					break;
				case 12:
					dl(i, a, n);
					break;
				case 13:
					dl(i, a, n), n && o & 4 && tl(i, a);
					break;
				case 22:
					a.memoizedState === null && dl(i, a, n), Pc(a, a.return);
					break;
				case 30: break;
				default: dl(i, a, n);
			}
			t = t.sibling;
		}
	}
	function fl(e, t) {
		var n = null;
		e !== null && e.memoizedState !== null && e.memoizedState.cachePool !== null && (n = e.memoizedState.cachePool.pool), e = null, t.memoizedState !== null && t.memoizedState.cachePool !== null && (e = t.memoizedState.cachePool.pool), e !== n && (e != null && e.refCount++, n != null && ra(n));
	}
	function pl(e, t) {
		e = null, t.alternate !== null && (e = t.alternate.memoizedState.cache), t = t.memoizedState.cache, t !== e && (t.refCount++, e != null && ra(e));
	}
	function ml(e, t, n, r) {
		if (t.subtreeFlags & 10256) for (t = t.child; t !== null;) hl(e, t, n, r), t = t.sibling;
	}
	function hl(e, t, n, r) {
		var i = t.flags;
		switch (t.tag) {
			case 0:
			case 11:
			case 15:
				ml(e, t, n, r), i & 2048 && Ac(9, t);
				break;
			case 1:
				ml(e, t, n, r);
				break;
			case 3:
				ml(e, t, n, r), i & 2048 && (e = null, t.alternate !== null && (e = t.alternate.memoizedState.cache), t = t.memoizedState.cache, t !== e && (t.refCount++, e != null && ra(e)));
				break;
			case 12:
				if (i & 2048) {
					ml(e, t, n, r), e = t.stateNode;
					try {
						var a = t.memoizedProps, o = a.id, s = a.onPostCommit;
						typeof s == "function" && s(o, t.alternate === null ? "mount" : "update", e.passiveEffectDuration, -0);
					} catch (e) {
						Z(t, t.return, e);
					}
				} else ml(e, t, n, r);
				break;
			case 13:
				ml(e, t, n, r);
				break;
			case 23: break;
			case 22:
				a = t.stateNode, o = t.alternate, t.memoizedState === null ? a._visibility & 2 ? ml(e, t, n, r) : (a._visibility |= 2, gl(e, t, n, r, !!(t.subtreeFlags & 10256))) : a._visibility & 2 ? ml(e, t, n, r) : _l(e, t), i & 2048 && fl(o, t);
				break;
			case 24:
				ml(e, t, n, r), i & 2048 && pl(t.alternate, t);
				break;
			default: ml(e, t, n, r);
		}
	}
	function gl(e, t, n, r, i) {
		for (i &&= !!(t.subtreeFlags & 10256), t = t.child; t !== null;) {
			var a = e, o = t, s = n, c = r, l = o.flags;
			switch (o.tag) {
				case 0:
				case 11:
				case 15:
					gl(a, o, s, c, i), Ac(8, o);
					break;
				case 23: break;
				case 22:
					var u = o.stateNode;
					o.memoizedState === null ? (u._visibility |= 2, gl(a, o, s, c, i)) : u._visibility & 2 ? gl(a, o, s, c, i) : _l(a, o), i && l & 2048 && fl(o.alternate, o);
					break;
				case 24:
					gl(a, o, s, c, i), i && l & 2048 && pl(o.alternate, o);
					break;
				default: gl(a, o, s, c, i);
			}
			t = t.sibling;
		}
	}
	function _l(e, t) {
		if (t.subtreeFlags & 10256) for (t = t.child; t !== null;) {
			var n = e, r = t, i = r.flags;
			switch (r.tag) {
				case 22:
					_l(n, r), i & 2048 && fl(r.alternate, r);
					break;
				case 24:
					_l(n, r), i & 2048 && pl(r.alternate, r);
					break;
				default: _l(n, r);
			}
			t = t.sibling;
		}
	}
	var vl = 8192;
	function yl(e) {
		if (e.subtreeFlags & vl) for (e = e.child; e !== null;) bl(e), e = e.sibling;
	}
	function bl(e) {
		switch (e.tag) {
			case 26:
				yl(e), e.flags & vl && e.memoizedState !== null && kf(al, e.memoizedState, e.memoizedProps);
				break;
			case 5:
				yl(e);
				break;
			case 3:
			case 4:
				var t = al;
				al = Zd(e.stateNode.containerInfo), yl(e), al = t;
				break;
			case 22:
				e.memoizedState === null && (t = e.alternate, t !== null && t.memoizedState !== null ? (t = vl, vl = 16777216, yl(e), vl = t) : yl(e));
				break;
			default: yl(e);
		}
	}
	function xl(e) {
		var t = e.alternate;
		if (t !== null && (e = t.child, e !== null)) {
			t.child = null;
			do
				t = e.sibling, e.sibling = null, e = t;
			while (e !== null);
		}
	}
	function Sl(e) {
		var t = e.deletions;
		if (e.flags & 16) {
			if (t !== null) for (var n = 0; n < t.length; n++) {
				var r = t[n];
				qc = r, Tl(r, e);
			}
			xl(e);
		}
		if (e.subtreeFlags & 10256) for (e = e.child; e !== null;) Cl(e), e = e.sibling;
	}
	function Cl(e) {
		switch (e.tag) {
			case 0:
			case 11:
			case 15:
				Sl(e), e.flags & 2048 && jc(9, e, e.return);
				break;
			case 3:
				Sl(e);
				break;
			case 12:
				Sl(e);
				break;
			case 22:
				var t = e.stateNode;
				e.memoizedState !== null && t._visibility & 2 && (e.return === null || e.return.tag !== 13) ? (t._visibility &= -3, wl(e)) : Sl(e);
				break;
			default: Sl(e);
		}
	}
	function wl(e) {
		var t = e.deletions;
		if (e.flags & 16) {
			if (t !== null) for (var n = 0; n < t.length; n++) {
				var r = t[n];
				qc = r, Tl(r, e);
			}
			xl(e);
		}
		for (e = e.child; e !== null;) {
			switch (t = e, t.tag) {
				case 0:
				case 11:
				case 15:
					jc(8, t, t.return), wl(t);
					break;
				case 22:
					n = t.stateNode, n._visibility & 2 && (n._visibility &= -3, wl(t));
					break;
				default: wl(t);
			}
			e = e.sibling;
		}
	}
	function Tl(e, t) {
		for (; qc !== null;) {
			var n = qc;
			switch (n.tag) {
				case 0:
				case 11:
				case 15:
					jc(8, n, t);
					break;
				case 23:
				case 22:
					if (n.memoizedState !== null && n.memoizedState.cachePool !== null) {
						var r = n.memoizedState.cachePool.pool;
						r != null && r.refCount++;
					}
					break;
				case 24: ra(n.memoizedState.cache);
			}
			if (r = n.child, r !== null) r.return = n, qc = r;
			else a: for (n = e; qc !== null;) {
				r = qc;
				var i = r.sibling, a = r.return;
				if (Xc(r), r === n) {
					qc = null;
					break a;
				}
				if (i !== null) {
					i.return = a, qc = i;
					break a;
				}
				qc = a;
			}
		}
	}
	var El = { getCacheForType: function(e) {
		var t = Yi(ta), n = t.data.get(e);
		return n === void 0 && (n = e(), t.data.set(e, n)), n;
	} }, Dl = typeof WeakMap == "function" ? WeakMap : Map, K = 0, q = null, J = null, Y = 0, X = 0, Ol = null, kl = !1, Al = !1, jl = !1, Ml = 0, Nl = 0, Pl = 0, Fl = 0, Il = 0, Ll = 0, Rl = 0, zl = null, Bl = null, Vl = !1, Hl = 0, Ul = Infinity, Wl = null, Gl = null, Kl = 0, ql = null, Jl = null, Yl = 0, Xl = 0, Zl = null, Ql = null, $l = 0, eu = null;
	function tu() {
		if (K & 2 && Y !== 0) return Y & -Y;
		if (P.T !== null) {
			var e = oa;
			return e === 0 ? Qu() : e;
		}
		return Je();
	}
	function nu() {
		Ll === 0 && (Ll = !(Y & 536870912) || B ? ze() : 536870912);
		var e = Os.current;
		return e !== null && (e.flags |= 32), Ll;
	}
	function ru(e, t, n) {
		(e === q && (X === 2 || X === 9) || e.cancelPendingCommit !== null) && (uu(e, 0), su(e, Y, Ll, !1)), He(e, n), K & 2 && e === q || (e === q && (!(K & 2) && (Fl |= n), Nl === 4 && su(e, Y, Ll, !1)), Wu(e));
	}
	function iu(e, t, n) {
		if (K & 6) throw Error(i(327));
		var r = !n && !(t & 124) && (t & e.expiredLanes) === 0 || Le(e, t), a = r ? _u(e, t) : hu(e, t, !0), o = r;
		do {
			if (a === 0) {
				Al && !r && su(e, t, 0, !1);
				break;
			}
			if (n = e.current.alternate, o && !ou(n)) a = hu(e, t, !1), o = !1;
			else {
				if (a === 2) {
					if (o = t, e.errorRecoveryDisabledLanes & o) var s = 0;
					else s = e.pendingLanes & -536870913, s = s === 0 ? s & 536870912 ? 536870912 : 0 : s;
					if (s !== 0) {
						t = s;
						a: {
							var c = e;
							a = zl;
							var l = c.current.memoizedState.isDehydrated;
							if (l && (uu(c, s).flags |= 256), s = hu(c, s, !1), s !== 2) {
								if (jl && !l) {
									c.errorRecoveryDisabledLanes |= o, Fl |= o, a = 4;
									break a;
								}
								o = Bl, Bl = a, o !== null && (Bl === null ? Bl = o : Bl.push.apply(Bl, o));
							}
							a = s;
						}
						if (o = !1, a !== 2) continue;
					}
				}
				if (a === 1) {
					uu(e, 0), su(e, t, 0, !0);
					break;
				}
				a: {
					switch (r = e, o = a, o) {
						case 0:
						case 1: throw Error(i(345));
						case 4: if ((t & 4194048) !== t) break;
						case 6:
							su(r, t, Ll, !kl);
							break a;
						case 2:
							Bl = null;
							break;
						case 3:
						case 5: break;
						default: throw Error(i(329));
					}
					if ((t & 62914560) === t && (a = Hl + 300 - _e(), 10 < a)) {
						if (su(r, t, Ll, !kl), Ie(r, 0, !0) !== 0) break a;
						r.timeoutHandle = Md(au.bind(null, r, n, Bl, Wl, Vl, t, Ll, Fl, Rl, kl, o, 2, -0, 0), a);
						break a;
					}
					au(r, n, Bl, Wl, Vl, t, Ll, Fl, Rl, kl, o, 0, -0, 0);
				}
				break;
			}
		} while (1);
		Wu(e);
	}
	function au(e, t, n, r, i, a, o, s, c, l, u, d, f, p) {
		e.timeoutHandle = -1, d = t.subtreeFlags, (d & 8192 || (d & 16785408) == 16785408) && (Df = {
			stylesheets: null,
			count: 0,
			unsuspend: Of
		}, bl(t), d = Af(), d !== null) ? (e.cancelPendingCommit = d(wu.bind(null, e, t, a, n, r, i, o, s, c, u, 1, f, p)), su(e, a, o, !l)) : wu(e, t, a, n, r, i, o, s, c);
	}
	function ou(e) {
		for (var t = e;;) {
			var n = t.tag;
			if ((n === 0 || n === 11 || n === 15) && t.flags & 16384 && (n = t.updateQueue, n !== null && (n = n.stores, n !== null))) for (var r = 0; r < n.length; r++) {
				var i = n[r], a = i.getSnapshot;
				i = i.value;
				try {
					if (!yr(a(), i)) return !1;
				} catch {
					return !1;
				}
			}
			if (n = t.child, t.subtreeFlags & 16384 && n !== null) n.return = t, t = n;
			else {
				if (t === e) break;
				for (; t.sibling === null;) {
					if (t.return === null || t.return === e) return !0;
					t = t.return;
				}
				t.sibling.return = t.return, t = t.sibling;
			}
		}
		return !0;
	}
	function su(e, t, n, r) {
		t &= ~Il, t &= ~Fl, e.suspendedLanes |= t, e.pingedLanes &= ~t, r && (e.warmLanes |= t), r = e.expirationTimes;
		for (var i = t; 0 < i;) {
			var a = 31 - ke(i), o = 1 << a;
			r[a] = -1, i &= ~o;
		}
		n !== 0 && We(e, n, t);
	}
	function cu() {
		return K & 6 ? !0 : (Gu(0, !1), !1);
	}
	function lu() {
		if (J !== null) {
			if (X === 0) var e = J.return;
			else e = J, Vi = Bi = null, ao(e), ys = null, bs = 0, e = J;
			for (; e !== null;) kc(e.alternate, e), e = e.return;
			J = null;
		}
	}
	function uu(e, t) {
		var n = e.timeoutHandle;
		n !== -1 && (e.timeoutHandle = -1, Nd(n)), n = e.cancelPendingCommit, n !== null && (e.cancelPendingCommit = null, n()), lu(), q = e, J = n = ci(e.current, null), Y = t, X = 0, Ol = null, kl = !1, Al = Le(e, t), jl = !1, Rl = Ll = Il = Fl = Pl = Nl = 0, Bl = zl = null, Vl = !1, t & 8 && (t |= t & 32);
		var r = e.entangledLanes;
		if (r !== 0) for (e = e.entanglements, r &= t; 0 < r;) {
			var i = 31 - ke(r), a = 1 << i;
			t |= e[i], r &= ~a;
		}
		return Ml = t, Qr(), n;
	}
	function du(e, t) {
		H = null, P.H = hs, t === ga || t === va ? (t = wa(), X = 3) : t === _a ? (t = wa(), X = 4) : X = t === Zs ? 8 : typeof t == "object" && t && typeof t.then == "function" ? 6 : 1, Ol = t, J === null && (Nl = 1, Gs(e, Jr(t, e.current)));
	}
	function fu() {
		var e = P.H;
		return P.H = hs, e === null ? hs : e;
	}
	function pu() {
		var e = P.A;
		return P.A = El, e;
	}
	function mu() {
		Nl = 4, kl || (Y & 4194048) !== Y && Os.current !== null || (Al = !0), !(Pl & 134217727) && !(Fl & 134217727) || q === null || su(q, Y, Ll, !1);
	}
	function hu(e, t, n) {
		var r = K;
		K |= 2;
		var i = fu(), a = pu();
		(q !== e || Y !== t) && (Wl = null, uu(e, t)), t = !1;
		var o = Nl;
		a: do
			try {
				if (X !== 0 && J !== null) {
					var s = J, c = Ol;
					switch (X) {
						case 8:
							lu(), o = 6;
							break a;
						case 3:
						case 2:
						case 9:
						case 6:
							Os.current === null && (t = !0);
							var l = X;
							if (X = 0, Ol = null, xu(e, s, c, l), n && Al) {
								o = 0;
								break a;
							}
							break;
						default: l = X, X = 0, Ol = null, xu(e, s, c, l);
					}
				}
				gu(), o = Nl;
				break;
			} catch (t) {
				du(e, t);
			}
		while (1);
		return t && e.shellSuspendCounter++, Vi = Bi = null, K = r, P.H = i, P.A = a, J === null && (q = null, Y = 0, Qr()), o;
	}
	function gu() {
		for (; J !== null;) yu(J);
	}
	function _u(e, t) {
		var n = K;
		K |= 2;
		var r = fu(), a = pu();
		q !== e || Y !== t ? (Wl = null, Ul = _e() + 500, uu(e, t)) : Al = Le(e, t);
		a: do
			try {
				if (X !== 0 && J !== null) {
					t = J;
					var o = Ol;
					b: switch (X) {
						case 1:
							X = 0, Ol = null, xu(e, t, o, 1);
							break;
						case 2:
						case 9:
							if (ba(o)) {
								X = 0, Ol = null, bu(t);
								break;
							}
							t = function() {
								X !== 2 && X !== 9 || q !== e || (X = 7), Wu(e);
							}, o.then(t, t);
							break a;
						case 3:
							X = 7;
							break a;
						case 4:
							X = 5;
							break a;
						case 7:
							ba(o) ? (X = 0, Ol = null, bu(t)) : (X = 0, Ol = null, xu(e, t, o, 7));
							break;
						case 5:
							var s = null;
							switch (J.tag) {
								case 26: s = J.memoizedState;
								case 5:
								case 27:
									var c = J;
									if (!s || Ef(s)) {
										X = 0, Ol = null;
										var l = c.sibling;
										if (l !== null) J = l;
										else {
											var u = c.return;
											u === null ? J = null : (J = u, Su(u));
										}
										break b;
									}
							}
							X = 0, Ol = null, xu(e, t, o, 5);
							break;
						case 6:
							X = 0, Ol = null, xu(e, t, o, 6);
							break;
						case 8:
							lu(), Nl = 6;
							break a;
						default: throw Error(i(462));
					}
				}
				vu();
				break;
			} catch (t) {
				du(e, t);
			}
		while (1);
		return Vi = Bi = null, P.H = r, P.A = a, K = n, J === null ? (q = null, Y = 0, Qr(), Nl) : 0;
	}
	function vu() {
		for (; J !== null && !he();) yu(J);
	}
	function yu(e) {
		var t = xc(e.alternate, e, Ml);
		e.memoizedProps = e.pendingProps, t === null ? Su(e) : J = t;
	}
	function bu(e) {
		var t = e, n = t.alternate;
		switch (t.tag) {
			case 15:
			case 0:
				t = sc(n, t, t.pendingProps, t.type, void 0, Y);
				break;
			case 11:
				t = sc(n, t, t.pendingProps, t.type.render, t.ref, Y);
				break;
			case 5: ao(t);
			default: kc(n, t), t = J = li(t, Ml), t = xc(n, t, Ml);
		}
		e.memoizedProps = e.pendingProps, t === null ? Su(e) : J = t;
	}
	function xu(e, t, n, r) {
		Vi = Bi = null, ao(t), ys = null, bs = 0;
		var i = t.return;
		try {
			if (Xs(e, i, t, n, Y)) {
				Nl = 1, Gs(e, Jr(n, e.current)), J = null;
				return;
			}
		} catch (t) {
			if (i !== null) throw J = i, t;
			Nl = 1, Gs(e, Jr(n, e.current)), J = null;
			return;
		}
		t.flags & 32768 ? (B || r === 1 ? e = !0 : Al || Y & 536870912 ? e = !1 : (kl = e = !0, (r === 2 || r === 9 || r === 3 || r === 6) && (r = Os.current, r !== null && r.tag === 13 && (r.flags |= 16384))), Cu(t, e)) : Su(t);
	}
	function Su(e) {
		var t = e;
		do {
			if (t.flags & 32768) {
				Cu(t, kl);
				return;
			}
			e = t.return;
			var n = Dc(t.alternate, t, Ml);
			if (n !== null) {
				J = n;
				return;
			}
			if (t = t.sibling, t !== null) {
				J = t;
				return;
			}
			J = t = e;
		} while (t !== null);
		Nl === 0 && (Nl = 5);
	}
	function Cu(e, t) {
		do {
			var n = Oc(e.alternate, e);
			if (n !== null) {
				n.flags &= 32767, J = n;
				return;
			}
			if (n = e.return, n !== null && (n.flags |= 32768, n.subtreeFlags = 0, n.deletions = null), !t && (e = e.sibling, e !== null)) {
				J = e;
				return;
			}
			J = e = n;
		} while (e !== null);
		Nl = 6, J = null;
	}
	function wu(e, t, n, r, a, o, s, c, l) {
		e.cancelPendingCommit = null;
		do
			ku();
		while (Kl !== 0);
		if (K & 6) throw Error(i(327));
		if (t !== null) {
			if (t === e.current) throw Error(i(177));
			if (o = t.lanes | t.childLanes, o |= Zr, Ue(e, n, o, s, c, l), e === q && (J = q = null, Y = 0), Jl = t, ql = e, Yl = n, Xl = o, Zl = a, Ql = r, t.subtreeFlags & 10256 || t.flags & 10256 ? (e.callbackNode = null, e.callbackPriority = 0, Lu(xe, function() {
				return Au(!0), null;
			})) : (e.callbackNode = null, e.callbackPriority = 0), r = !!(t.flags & 13878), t.subtreeFlags & 13878 || r) {
				r = P.T, P.T = null, a = F.p, F.p = 2, s = K, K |= 4;
				try {
					Jc(e, t, n);
				} finally {
					K = s, F.p = a, P.T = r;
				}
			}
			Kl = 1, Tu(), Eu(), Du();
		}
	}
	function Tu() {
		if (Kl === 1) {
			Kl = 0;
			var e = ql, t = Jl, n = !!(t.flags & 13878);
			if (t.subtreeFlags & 13878 || n) {
				n = P.T, P.T = null;
				var r = F.p;
				F.p = 2;
				var i = K;
				K |= 4;
				try {
					ol(t, e);
					var a = Td, o = wr(e.containerInfo), s = a.focusedElem, c = a.selectionRange;
					if (o !== s && s && s.ownerDocument && Cr(s.ownerDocument.documentElement, s)) {
						if (c !== null && Tr(s)) {
							var l = c.start, u = c.end;
							if (u === void 0 && (u = l), "selectionStart" in s) s.selectionStart = l, s.selectionEnd = Math.min(u, s.value.length);
							else {
								var d = s.ownerDocument || document, f = d && d.defaultView || window;
								if (f.getSelection) {
									var p = f.getSelection(), m = s.textContent.length, h = Math.min(c.start, m), g = c.end === void 0 ? h : Math.min(c.end, m);
									!p.extend && h > g && (o = g, g = h, h = o);
									var _ = Sr(s, h), v = Sr(s, g);
									if (_ && v && (p.rangeCount !== 1 || p.anchorNode !== _.node || p.anchorOffset !== _.offset || p.focusNode !== v.node || p.focusOffset !== v.offset)) {
										var y = d.createRange();
										y.setStart(_.node, _.offset), p.removeAllRanges(), h > g ? (p.addRange(y), p.extend(v.node, v.offset)) : (y.setEnd(v.node, v.offset), p.addRange(y));
									}
								}
							}
						}
						for (d = [], p = s; p = p.parentNode;) p.nodeType === 1 && d.push({
							element: p,
							left: p.scrollLeft,
							top: p.scrollTop
						});
						for (typeof s.focus == "function" && s.focus(), s = 0; s < d.length; s++) {
							var b = d[s];
							b.element.scrollLeft = b.left, b.element.scrollTop = b.top;
						}
					}
					Uf = !!wd, Td = wd = null;
				} finally {
					K = i, F.p = r, P.T = n;
				}
			}
			e.current = t, Kl = 2;
		}
	}
	function Eu() {
		if (Kl === 2) {
			Kl = 0;
			var e = ql, t = Jl, n = !!(t.flags & 8772);
			if (t.subtreeFlags & 8772 || n) {
				n = P.T, P.T = null;
				var r = F.p;
				F.p = 2;
				var i = K;
				K |= 4;
				try {
					Yc(e, t.alternate, t);
				} finally {
					K = i, F.p = r, P.T = n;
				}
			}
			Kl = 3;
		}
	}
	function Du() {
		if (Kl === 4 || Kl === 3) {
			Kl = 0, ge();
			var e = ql, t = Jl, n = Yl, r = Ql;
			t.subtreeFlags & 10256 || t.flags & 10256 ? Kl = 5 : (Kl = 0, Jl = ql = null, Ou(e, e.pendingLanes));
			var i = e.pendingLanes;
			if (i === 0 && (Gl = null), qe(n), t = t.stateNode, De && typeof De.onCommitFiberRoot == "function") try {
				De.onCommitFiberRoot(Ee, t, void 0, (t.current.flags & 128) == 128);
			} catch {}
			if (r !== null) {
				t = P.T, i = F.p, F.p = 2, P.T = null;
				try {
					for (var a = e.onRecoverableError, o = 0; o < r.length; o++) {
						var s = r[o];
						a(s.value, { componentStack: s.stack });
					}
				} finally {
					P.T = t, F.p = i;
				}
			}
			Yl & 3 && ku(), Wu(e), i = e.pendingLanes, n & 4194090 && i & 42 ? e === eu ? $l++ : ($l = 0, eu = e) : $l = 0, Gu(0, !1);
		}
	}
	function Ou(e, t) {
		(e.pooledCacheLanes &= t) === 0 && (t = e.pooledCache, t != null && (e.pooledCache = null, ra(t)));
	}
	function ku(e) {
		return Tu(), Eu(), Du(), Au(e);
	}
	function Au() {
		if (Kl !== 5) return !1;
		var e = ql, t = Xl;
		Xl = 0;
		var n = qe(Yl), r = P.T, a = F.p;
		try {
			F.p = 32 > n ? 32 : n, P.T = null, n = Zl, Zl = null;
			var o = ql, s = Yl;
			if (Kl = 0, Jl = ql = null, Yl = 0, K & 6) throw Error(i(331));
			var c = K;
			if (K |= 4, Cl(o.current), hl(o, o.current, s, n), K = c, Gu(0, !1), De && typeof De.onPostCommitFiberRoot == "function") try {
				De.onPostCommitFiberRoot(Ee, o);
			} catch {}
			return !0;
		} finally {
			F.p = a, P.T = r, Ou(e, t);
		}
	}
	function ju(e, t, n) {
		t = Jr(n, t), t = qs(e.stateNode, t, 2), e = ka(e, t, 2), e !== null && (He(e, 2), Wu(e));
	}
	function Z(e, t, n) {
		if (e.tag === 3) ju(e, e, n);
		else for (; t !== null;) {
			if (t.tag === 3) {
				ju(t, e, n);
				break;
			}
			if (t.tag === 1) {
				var r = t.stateNode;
				if (typeof t.type.getDerivedStateFromError == "function" || typeof r.componentDidCatch == "function" && (Gl === null || !Gl.has(r))) {
					e = Jr(n, e), n = Js(2), r = ka(t, n, 2), r !== null && (Ys(n, r, t, e), He(r, 2), Wu(r));
					break;
				}
			}
			t = t.return;
		}
	}
	function Mu(e, t, n) {
		var r = e.pingCache;
		if (r === null) {
			r = e.pingCache = new Dl();
			var i = /* @__PURE__ */ new Set();
			r.set(t, i);
		} else i = r.get(t), i === void 0 && (i = /* @__PURE__ */ new Set(), r.set(t, i));
		i.has(n) || (jl = !0, i.add(n), e = Nu.bind(null, e, t, n), t.then(e, e));
	}
	function Nu(e, t, n) {
		var r = e.pingCache;
		r !== null && r.delete(t), e.pingedLanes |= e.suspendedLanes & n, e.warmLanes &= ~n, q === e && (Y & n) === n && (Nl === 4 || Nl === 3 && (Y & 62914560) === Y && 300 > _e() - Hl ? !(K & 2) && uu(e, 0) : Il |= n, Rl === Y && (Rl = 0)), Wu(e);
	}
	function Pu(e, t) {
		t === 0 && (t = Be()), e = ti(e, t), e !== null && (He(e, t), Wu(e));
	}
	function Fu(e) {
		var t = e.memoizedState, n = 0;
		t !== null && (n = t.retryLane), Pu(e, n);
	}
	function Iu(e, t) {
		var n = 0;
		switch (e.tag) {
			case 13:
				var r = e.stateNode, a = e.memoizedState;
				a !== null && (n = a.retryLane);
				break;
			case 19:
				r = e.stateNode;
				break;
			case 22:
				r = e.stateNode._retryCache;
				break;
			default: throw Error(i(314));
		}
		r !== null && r.delete(t), Pu(e, n);
	}
	function Lu(e, t) {
		return pe(e, t);
	}
	var Ru = null, zu = null, Bu = !1, Vu = !1, Hu = !1, Uu = 0;
	function Wu(e) {
		e !== zu && e.next === null && (zu === null ? Ru = zu = e : zu = zu.next = e), Vu = !0, Bu || (Bu = !0, Zu());
	}
	function Gu(e, t) {
		if (!Hu && Vu) {
			Hu = !0;
			do
				for (var n = !1, r = Ru; r !== null;) {
					if (!t) {
						if (e !== 0) {
							var i = r.pendingLanes;
							if (i === 0) var a = 0;
							else {
								var o = r.suspendedLanes, s = r.pingedLanes;
								a = (1 << 31 - ke(42 | e) + 1) - 1, a &= i & ~(o & ~s), a = a & 201326741 ? a & 201326741 | 1 : a ? a | 2 : 0;
							}
							a !== 0 && (n = !0, Xu(r, a));
						} else a = Y, a = Ie(r, r === q ? a : 0, r.cancelPendingCommit !== null || r.timeoutHandle !== -1), !(a & 3) || Le(r, a) || (n = !0, Xu(r, a));
					}
					r = r.next;
				}
			while (n);
			Hu = !1;
		}
	}
	function Ku() {
		qu();
	}
	function qu() {
		Vu = Bu = !1;
		var e = 0;
		Uu !== 0 && (jd() && (e = Uu), Uu = 0);
		for (var t = _e(), n = null, r = Ru; r !== null;) {
			var i = r.next, a = Ju(r, t);
			a === 0 ? (r.next = null, n === null ? Ru = i : n.next = i, i === null && (zu = n)) : (n = r, (e !== 0 || a & 3) && (Vu = !0)), r = i;
		}
		Gu(e, !1);
	}
	function Ju(e, t) {
		for (var n = e.suspendedLanes, r = e.pingedLanes, i = e.expirationTimes, a = e.pendingLanes & -62914561; 0 < a;) {
			var o = 31 - ke(a), s = 1 << o, c = i[o];
			c === -1 ? ((s & n) === 0 || (s & r) !== 0) && (i[o] = Re(s, t)) : c <= t && (e.expiredLanes |= s), a &= ~s;
		}
		if (t = q, n = Y, n = Ie(e, e === t ? n : 0, e.cancelPendingCommit !== null || e.timeoutHandle !== -1), r = e.callbackNode, n === 0 || e === t && (X === 2 || X === 9) || e.cancelPendingCommit !== null) return r !== null && r !== null && me(r), e.callbackNode = null, e.callbackPriority = 0;
		if (!(n & 3) || Le(e, n)) {
			if (t = n & -n, t === e.callbackPriority) return t;
			switch (r !== null && me(r), qe(n)) {
				case 2:
				case 8:
					n = be;
					break;
				case 32:
					n = xe;
					break;
				case 268435456:
					n = Ce;
					break;
				default: n = xe;
			}
			return r = Yu.bind(null, e), n = pe(n, r), e.callbackPriority = t, e.callbackNode = n, t;
		}
		return r !== null && r !== null && me(r), e.callbackPriority = 2, e.callbackNode = null, 2;
	}
	function Yu(e, t) {
		if (Kl !== 0 && Kl !== 5) return e.callbackNode = null, e.callbackPriority = 0, null;
		var n = e.callbackNode;
		if (ku(!0) && e.callbackNode !== n) return null;
		var r = Y;
		return r = Ie(e, e === q ? r : 0, e.cancelPendingCommit !== null || e.timeoutHandle !== -1), r === 0 ? null : (iu(e, r, t), Ju(e, _e()), e.callbackNode != null && e.callbackNode === n ? Yu.bind(null, e) : null);
	}
	function Xu(e, t) {
		if (ku()) return null;
		iu(e, t, !0);
	}
	function Zu() {
		Fd(function() {
			K & 6 ? pe(ye, Ku) : qu();
		});
	}
	function Qu() {
		return Uu === 0 && (Uu = ze()), Uu;
	}
	function $u(e) {
		return e == null || typeof e == "symbol" || typeof e == "boolean" ? null : typeof e == "function" ? e : Xt("" + e);
	}
	function ed(e, t) {
		var n = t.ownerDocument.createElement("input");
		return n.name = t.name, n.value = t.value, e.id && n.setAttribute("form", e.id), t.parentNode.insertBefore(n, t), e = new FormData(e), n.parentNode.removeChild(n), e;
	}
	function td(e, t, n, r, i) {
		if (t === "submit" && n && n.stateNode === i) {
			var a = $u((i[Qe] || null).action), o = r.submitter;
			o && (t = (t = o[Qe] || null) ? $u(t.formAction) : o.getAttribute("formAction"), t !== null && (a = t, o = null));
			var s = new vn("action", "action", null, r, i);
			e.push({
				event: s,
				listeners: [{
					instance: null,
					listener: function() {
						if (r.defaultPrevented) {
							if (Uu !== 0) {
								var e = o ? ed(i, o) : new FormData(i);
								ts(n, {
									pending: !0,
									data: e,
									method: i.method,
									action: a
								}, null, e);
							}
						} else typeof a == "function" && (s.preventDefault(), e = o ? ed(i, o) : new FormData(i), ts(n, {
							pending: !0,
							data: e,
							method: i.method,
							action: a
						}, a, e));
					},
					currentTarget: i
				}]
			});
		}
	}
	for (var nd = 0; nd < Gr.length; nd++) {
		var rd = Gr[nd];
		Kr(rd.toLowerCase(), "on" + (rd[0].toUpperCase() + rd.slice(1)));
	}
	Kr(Lr, "onAnimationEnd"), Kr(Rr, "onAnimationIteration"), Kr(zr, "onAnimationStart"), Kr("dblclick", "onDoubleClick"), Kr("focusin", "onFocus"), Kr("focusout", "onBlur"), Kr(Br, "onTransitionRun"), Kr(Vr, "onTransitionStart"), Kr(Hr, "onTransitionCancel"), Kr(Ur, "onTransitionEnd"), mt("onMouseEnter", ["mouseout", "mouseover"]), mt("onMouseLeave", ["mouseout", "mouseover"]), mt("onPointerEnter", ["pointerout", "pointerover"]), mt("onPointerLeave", ["pointerout", "pointerover"]), pt("onChange", "change click focusin focusout input keydown keyup selectionchange".split(" ")), pt("onSelect", "focusout contextmenu dragend focusin keydown keyup mousedown mouseup selectionchange".split(" ")), pt("onBeforeInput", [
		"compositionend",
		"keypress",
		"textInput",
		"paste"
	]), pt("onCompositionEnd", "compositionend focusout keydown keypress keyup mousedown".split(" ")), pt("onCompositionStart", "compositionstart focusout keydown keypress keyup mousedown".split(" ")), pt("onCompositionUpdate", "compositionupdate focusout keydown keypress keyup mousedown".split(" "));
	var id = "abort canplay canplaythrough durationchange emptied encrypted ended error loadeddata loadedmetadata loadstart pause play playing progress ratechange resize seeked seeking stalled suspend timeupdate volumechange waiting".split(" "), ad = new Set("beforetoggle cancel close invalid load scroll scrollend toggle".split(" ").concat(id));
	function od(e, t) {
		t = !!(t & 4);
		for (var n = 0; n < e.length; n++) {
			var r = e[n], i = r.event;
			r = r.listeners;
			a: {
				var a = void 0;
				if (t) for (var o = r.length - 1; 0 <= o; o--) {
					var s = r[o], c = s.instance, l = s.currentTarget;
					if (s = s.listener, c !== a && i.isPropagationStopped()) break a;
					a = s, i.currentTarget = l;
					try {
						a(i);
					} catch (e) {
						Vs(e);
					}
					i.currentTarget = null, a = c;
				}
				else for (o = 0; o < r.length; o++) {
					if (s = r[o], c = s.instance, l = s.currentTarget, s = s.listener, c !== a && i.isPropagationStopped()) break a;
					a = s, i.currentTarget = l;
					try {
						a(i);
					} catch (e) {
						Vs(e);
					}
					i.currentTarget = null, a = c;
				}
			}
		}
	}
	function Q(e, t) {
		var n = t[et];
		n === void 0 && (n = t[et] = /* @__PURE__ */ new Set());
		var r = e + "__bubble";
		n.has(r) || (ud(t, e, 2, !1), n.add(r));
	}
	function sd(e, t, n) {
		var r = 0;
		t && (r |= 4), ud(n, e, r, t);
	}
	var cd = "_reactListening" + Math.random().toString(36).slice(2);
	function ld(e) {
		if (!e[cd]) {
			e[cd] = !0, dt.forEach(function(t) {
				t !== "selectionchange" && (ad.has(t) || sd(t, !1, e), sd(t, !0, e));
			});
			var t = e.nodeType === 9 ? e : e.ownerDocument;
			t === null || t[cd] || (t[cd] = !0, sd("selectionchange", !1, t));
		}
	}
	function ud(e, t, n, r) {
		switch (Xf(t)) {
			case 2:
				var i = Wf;
				break;
			case 8:
				i = Gf;
				break;
			default: i = Kf;
		}
		n = i.bind(null, t, n, e), i = void 0, !sn || t !== "touchstart" && t !== "touchmove" && t !== "wheel" || (i = !0), r ? i === void 0 ? e.addEventListener(t, n, !0) : e.addEventListener(t, n, {
			capture: !0,
			passive: i
		}) : i === void 0 ? e.addEventListener(t, n, !1) : e.addEventListener(t, n, { passive: i });
	}
	function dd(e, t, n, r, i) {
		var a = r;
		if (!(t & 1) && !(t & 2) && r !== null) a: for (;;) {
			if (r === null) return;
			var s = r.tag;
			if (s === 3 || s === 4) {
				var c = r.stateNode.containerInfo;
				if (c === i) break;
				if (s === 4) for (s = r.return; s !== null;) {
					var l = s.tag;
					if ((l === 3 || l === 4) && s.stateNode.containerInfo === i) return;
					s = s.return;
				}
				for (; c !== null;) {
					if (s = ot(c), s === null) return;
					if (l = s.tag, l === 5 || l === 6 || l === 26 || l === 27) {
						r = a = s;
						continue a;
					}
					c = c.parentNode;
				}
			}
			r = r.return;
		}
		rn(function() {
			var r = a, i = Qt(n), s = [];
			a: {
				var c = Wr.get(e);
				if (c !== void 0) {
					var l = vn, u = e;
					switch (e) {
						case "keypress": if (pn(n) === 0) break a;
						case "keydown":
						case "keyup":
							l = In;
							break;
						case "focusin":
							u = "focus", l = Dn;
							break;
						case "focusout":
							u = "blur", l = Dn;
							break;
						case "beforeblur":
						case "afterblur":
							l = Dn;
							break;
						case "click": if (n.button === 2) break a;
						case "auxclick":
						case "dblclick":
						case "mousedown":
						case "mousemove":
						case "mouseup":
						case "mouseout":
						case "mouseover":
						case "contextmenu":
							l = Tn;
							break;
						case "drag":
						case "dragend":
						case "dragenter":
						case "dragexit":
						case "dragleave":
						case "dragover":
						case "dragstart":
						case "drop":
							l = En;
							break;
						case "touchcancel":
						case "touchend":
						case "touchmove":
						case "touchstart":
							l = Rn;
							break;
						case Lr:
						case Rr:
						case zr:
							l = On;
							break;
						case Ur:
							l = zn;
							break;
						case "scroll":
						case "scrollend":
							l = bn;
							break;
						case "wheel":
							l = Bn;
							break;
						case "copy":
						case "cut":
						case "paste":
							l = kn;
							break;
						case "gotpointercapture":
						case "lostpointercapture":
						case "pointercancel":
						case "pointerdown":
						case "pointermove":
						case "pointerout":
						case "pointerover":
						case "pointerup":
							l = Ln;
							break;
						case "toggle":
						case "beforetoggle": l = Vn;
					}
					var d = !!(t & 4), f = !d && (e === "scroll" || e === "scrollend"), p = d ? c === null ? null : c + "Capture" : c;
					d = [];
					for (var m = r, h; m !== null;) {
						var g = m;
						if (h = g.stateNode, g = g.tag, g !== 5 && g !== 26 && g !== 27 || h === null || p === null || (g = an(m, p), g != null && d.push(fd(m, g, h))), f) break;
						m = m.return;
					}
					0 < d.length && (c = new l(c, u, null, n, i), s.push({
						event: c,
						listeners: d
					}));
				}
			}
			if (!(t & 7)) {
				a: {
					if (c = e === "mouseover" || e === "pointerover", l = e === "mouseout" || e === "pointerout", c && n !== Zt && (u = n.relatedTarget || n.fromElement) && (ot(u) || u[$e])) break a;
					if ((l || c) && (c = i.window === i ? i : (c = i.ownerDocument) ? c.defaultView || c.parentWindow : window, l ? (u = n.relatedTarget || n.toElement, l = r, u = u ? ot(u) : null, u !== null && (f = o(u), d = u.tag, u !== f || d !== 5 && d !== 27 && d !== 6) && (u = null)) : (l = null, u = r), l !== u)) {
						if (d = Tn, g = "onMouseLeave", p = "onMouseEnter", m = "mouse", (e === "pointerout" || e === "pointerover") && (d = Ln, g = "onPointerLeave", p = "onPointerEnter", m = "pointer"), f = l == null ? c : ct(l), h = u == null ? c : ct(u), c = new d(g, m + "leave", l, n, i), c.target = f, c.relatedTarget = h, g = null, ot(i) === r && (d = new d(p, m + "enter", u, n, i), d.target = h, d.relatedTarget = f, g = d), f = g, l && u) b: {
							for (d = l, p = u, m = 0, h = d; h; h = md(h)) m++;
							for (h = 0, g = p; g; g = md(g)) h++;
							for (; 0 < m - h;) d = md(d), m--;
							for (; 0 < h - m;) p = md(p), h--;
							for (; m--;) {
								if (d === p || p !== null && d === p.alternate) break b;
								d = md(d), p = md(p);
							}
							d = null;
						}
						else d = null;
						l !== null && hd(s, c, l, d, !1), u !== null && f !== null && hd(s, f, u, d, !0);
					}
				}
				a: {
					if (c = r ? ct(r) : window, l = c.nodeName && c.nodeName.toLowerCase(), l === "select" || l === "input" && c.type === "file") var _ = sr;
					else if (tr(c)) {
						if (cr) _ = _r;
						else {
							_ = hr;
							var v = mr;
						}
					} else l = c.nodeName, !l || l.toLowerCase() !== "input" || c.type !== "checkbox" && c.type !== "radio" ? r && qt(r.elementType) && (_ = sr) : _ = gr;
					if (_ &&= _(e, r)) {
						nr(s, _, n, i);
						break a;
					}
					v && v(e, c, r), e === "focusout" && r && c.type === "number" && r.memoizedProps.value != null && zt(c, "number", c.value);
				}
				switch (v = r ? ct(r) : window, e) {
					case "focusin":
						(tr(v) || v.contentEditable === "true") && (Dr = v, Or = r, kr = null);
						break;
					case "focusout":
						kr = Or = Dr = null;
						break;
					case "mousedown":
						Ar = !0;
						break;
					case "contextmenu":
					case "mouseup":
					case "dragend":
						Ar = !1, jr(s, n, i);
						break;
					case "selectionchange": if (Er) break;
					case "keydown":
					case "keyup": jr(s, n, i);
				}
				var y;
				if (Un) b: {
					switch (e) {
						case "compositionstart":
							var b = "onCompositionStart";
							break b;
						case "compositionend":
							b = "onCompositionEnd";
							break b;
						case "compositionupdate":
							b = "onCompositionUpdate";
							break b;
					}
					b = void 0;
				}
				else Zn ? Yn(e, n) && (b = "onCompositionEnd") : e === "keydown" && n.keyCode === 229 && (b = "onCompositionStart");
				b && (Kn && n.locale !== "ko" && (Zn || b !== "onCompositionStart" ? b === "onCompositionEnd" && Zn && (y = fn()) : (ln = i, un = "value" in ln ? ln.value : ln.textContent, Zn = !0)), v = pd(r, b), 0 < v.length && (b = new An(b, e, null, n, i), s.push({
					event: b,
					listeners: v
				}), y ? b.data = y : (y = Xn(n), y !== null && (b.data = y)))), (y = Gn ? Qn(e, n) : $n(e, n)) && (b = pd(r, "onBeforeInput"), 0 < b.length && (v = new An("onBeforeInput", "beforeinput", null, n, i), s.push({
					event: v,
					listeners: b
				}), v.data = y)), td(s, e, r, n, i);
			}
			od(s, t);
		});
	}
	function fd(e, t, n) {
		return {
			instance: e,
			listener: t,
			currentTarget: n
		};
	}
	function pd(e, t) {
		for (var n = t + "Capture", r = []; e !== null;) {
			var i = e, a = i.stateNode;
			if (i = i.tag, i !== 5 && i !== 26 && i !== 27 || a === null || (i = an(e, n), i != null && r.unshift(fd(e, i, a)), i = an(e, t), i != null && r.push(fd(e, i, a))), e.tag === 3) return r;
			e = e.return;
		}
		return [];
	}
	function md(e) {
		if (e === null) return null;
		do
			e = e.return;
		while (e && e.tag !== 5 && e.tag !== 27);
		return e || null;
	}
	function hd(e, t, n, r, i) {
		for (var a = t._reactName, o = []; n !== null && n !== r;) {
			var s = n, c = s.alternate, l = s.stateNode;
			if (s = s.tag, c !== null && c === r) break;
			s !== 5 && s !== 26 && s !== 27 || l === null || (c = l, i ? (l = an(n, a), l != null && o.unshift(fd(n, l, c))) : i || (l = an(n, a), l != null && o.push(fd(n, l, c)))), n = n.return;
		}
		o.length !== 0 && e.push({
			event: t,
			listeners: o
		});
	}
	var gd = /\r\n?/g, _d = /\u0000|\uFFFD/g;
	function vd(e) {
		return (typeof e == "string" ? e : "" + e).replace(gd, "\n").replace(_d, "");
	}
	function yd(e, t) {
		return t = vd(t), vd(e) === t;
	}
	function bd() {}
	function $(e, t, n, r, a, o) {
		switch (n) {
			case "children":
				typeof r == "string" ? t === "body" || t === "textarea" && r === "" || Ut(e, r) : (typeof r == "number" || typeof r == "bigint") && t !== "body" && Ut(e, "" + r);
				break;
			case "className":
				bt(e, "class", r);
				break;
			case "tabIndex":
				bt(e, "tabindex", r);
				break;
			case "dir":
			case "role":
			case "viewBox":
			case "width":
			case "height":
				bt(e, n, r);
				break;
			case "style":
				Kt(e, r, o);
				break;
			case "data": if (t !== "object") {
				bt(e, "data", r);
				break;
			}
			case "src":
			case "href":
				if (r === "" && (t !== "a" || n !== "href") || r == null || typeof r == "function" || typeof r == "symbol" || typeof r == "boolean") {
					e.removeAttribute(n);
					break;
				}
				r = Xt("" + r), e.setAttribute(n, r);
				break;
			case "action":
			case "formAction":
				if (typeof r == "function") {
					e.setAttribute(n, "javascript:throw new Error('A React form was unexpectedly submitted. If you called form.submit() manually, consider using form.requestSubmit() instead. If you\\'re trying to use event.stopPropagation() in a submit event handler, consider also calling event.preventDefault().')");
					break;
				}
				if (typeof o == "function" && (n === "formAction" ? (t !== "input" && $(e, t, "name", a.name, a, null), $(e, t, "formEncType", a.formEncType, a, null), $(e, t, "formMethod", a.formMethod, a, null), $(e, t, "formTarget", a.formTarget, a, null)) : ($(e, t, "encType", a.encType, a, null), $(e, t, "method", a.method, a, null), $(e, t, "target", a.target, a, null))), r == null || typeof r == "symbol" || typeof r == "boolean") {
					e.removeAttribute(n);
					break;
				}
				r = Xt("" + r), e.setAttribute(n, r);
				break;
			case "onClick":
				r != null && (e.onclick = bd);
				break;
			case "onScroll":
				r != null && Q("scroll", e);
				break;
			case "onScrollEnd":
				r != null && Q("scrollend", e);
				break;
			case "dangerouslySetInnerHTML":
				if (r != null) {
					if (typeof r != "object" || !("__html" in r)) throw Error(i(61));
					if (n = r.__html, n != null) {
						if (a.children != null) throw Error(i(60));
						e.innerHTML = n;
					}
				}
				break;
			case "multiple":
				e.multiple = r && typeof r != "function" && typeof r != "symbol";
				break;
			case "muted":
				e.muted = r && typeof r != "function" && typeof r != "symbol";
				break;
			case "suppressContentEditableWarning":
			case "suppressHydrationWarning":
			case "defaultValue":
			case "defaultChecked":
			case "innerHTML":
			case "ref": break;
			case "autoFocus": break;
			case "xlinkHref":
				if (r == null || typeof r == "function" || typeof r == "boolean" || typeof r == "symbol") {
					e.removeAttribute("xlink:href");
					break;
				}
				n = Xt("" + r), e.setAttributeNS("http://www.w3.org/1999/xlink", "xlink:href", n);
				break;
			case "contentEditable":
			case "spellCheck":
			case "draggable":
			case "value":
			case "autoReverse":
			case "externalResourcesRequired":
			case "focusable":
			case "preserveAlpha":
				r != null && typeof r != "function" && typeof r != "symbol" ? e.setAttribute(n, "" + r) : e.removeAttribute(n);
				break;
			case "inert":
			case "allowFullScreen":
			case "async":
			case "autoPlay":
			case "controls":
			case "default":
			case "defer":
			case "disabled":
			case "disablePictureInPicture":
			case "disableRemotePlayback":
			case "formNoValidate":
			case "hidden":
			case "loop":
			case "noModule":
			case "noValidate":
			case "open":
			case "playsInline":
			case "readOnly":
			case "required":
			case "reversed":
			case "scoped":
			case "seamless":
			case "itemScope":
				r && typeof r != "function" && typeof r != "symbol" ? e.setAttribute(n, "") : e.removeAttribute(n);
				break;
			case "capture":
			case "download":
				!0 === r ? e.setAttribute(n, "") : !1 !== r && r != null && typeof r != "function" && typeof r != "symbol" ? e.setAttribute(n, r) : e.removeAttribute(n);
				break;
			case "cols":
			case "rows":
			case "size":
			case "span":
				r != null && typeof r != "function" && typeof r != "symbol" && !isNaN(r) && 1 <= r ? e.setAttribute(n, r) : e.removeAttribute(n);
				break;
			case "rowSpan":
			case "start":
				r == null || typeof r == "function" || typeof r == "symbol" || isNaN(r) ? e.removeAttribute(n) : e.setAttribute(n, r);
				break;
			case "popover":
				Q("beforetoggle", e), Q("toggle", e), yt(e, "popover", r);
				break;
			case "xlinkActuate":
				xt(e, "http://www.w3.org/1999/xlink", "xlink:actuate", r);
				break;
			case "xlinkArcrole":
				xt(e, "http://www.w3.org/1999/xlink", "xlink:arcrole", r);
				break;
			case "xlinkRole":
				xt(e, "http://www.w3.org/1999/xlink", "xlink:role", r);
				break;
			case "xlinkShow":
				xt(e, "http://www.w3.org/1999/xlink", "xlink:show", r);
				break;
			case "xlinkTitle":
				xt(e, "http://www.w3.org/1999/xlink", "xlink:title", r);
				break;
			case "xlinkType":
				xt(e, "http://www.w3.org/1999/xlink", "xlink:type", r);
				break;
			case "xmlBase":
				xt(e, "http://www.w3.org/XML/1998/namespace", "xml:base", r);
				break;
			case "xmlLang":
				xt(e, "http://www.w3.org/XML/1998/namespace", "xml:lang", r);
				break;
			case "xmlSpace":
				xt(e, "http://www.w3.org/XML/1998/namespace", "xml:space", r);
				break;
			case "is":
				yt(e, "is", r);
				break;
			case "innerText":
			case "textContent": break;
			default: (!(2 < n.length) || n[0] !== "o" && n[0] !== "O" || n[1] !== "n" && n[1] !== "N") && (n = Jt.get(n) || n, yt(e, n, r));
		}
	}
	function xd(e, t, n, r, a, o) {
		switch (n) {
			case "style":
				Kt(e, r, o);
				break;
			case "dangerouslySetInnerHTML":
				if (r != null) {
					if (typeof r != "object" || !("__html" in r)) throw Error(i(61));
					if (n = r.__html, n != null) {
						if (a.children != null) throw Error(i(60));
						e.innerHTML = n;
					}
				}
				break;
			case "children":
				typeof r == "string" ? Ut(e, r) : (typeof r == "number" || typeof r == "bigint") && Ut(e, "" + r);
				break;
			case "onScroll":
				r != null && Q("scroll", e);
				break;
			case "onScrollEnd":
				r != null && Q("scrollend", e);
				break;
			case "onClick":
				r != null && (e.onclick = bd);
				break;
			case "suppressContentEditableWarning":
			case "suppressHydrationWarning":
			case "innerHTML":
			case "ref": break;
			case "innerText":
			case "textContent": break;
			default: if (!ft.hasOwnProperty(n)) a: {
				if (n[0] === "o" && n[1] === "n" && (a = n.endsWith("Capture"), t = n.slice(2, a ? n.length - 7 : void 0), o = e[Qe] || null, o = o == null ? null : o[n], typeof o == "function" && e.removeEventListener(t, o, a), typeof r == "function")) {
					typeof o != "function" && o !== null && (n in e ? e[n] = null : e.hasAttribute(n) && e.removeAttribute(n)), e.addEventListener(t, r, a);
					break a;
				}
				n in e ? e[n] = r : !0 === r ? e.setAttribute(n, "") : yt(e, n, r);
			}
		}
	}
	function Sd(e, t, n) {
		switch (t) {
			case "div":
			case "span":
			case "svg":
			case "path":
			case "a":
			case "g":
			case "p":
			case "li": break;
			case "img":
				Q("error", e), Q("load", e);
				var r = !1, a = !1, o;
				for (o in n) if (n.hasOwnProperty(o)) {
					var s = n[o];
					if (s != null) switch (o) {
						case "src":
							r = !0;
							break;
						case "srcSet":
							a = !0;
							break;
						case "children":
						case "dangerouslySetInnerHTML": throw Error(i(137, t));
						default: $(e, t, o, s, n, null);
					}
				}
				a && $(e, t, "srcSet", n.srcSet, n, null), r && $(e, t, "src", n.src, n, null);
				return;
			case "input":
				Q("invalid", e);
				var c = o = s = a = null, l = null, u = null;
				for (r in n) if (n.hasOwnProperty(r)) {
					var d = n[r];
					if (d != null) switch (r) {
						case "name":
							a = d;
							break;
						case "type":
							s = d;
							break;
						case "checked":
							l = d;
							break;
						case "defaultChecked":
							u = d;
							break;
						case "value":
							o = d;
							break;
						case "defaultValue":
							c = d;
							break;
						case "children":
						case "dangerouslySetInnerHTML":
							if (d != null) throw Error(i(137, t));
							break;
						default: $(e, t, r, d, n, null);
					}
				}
				Rt(e, o, c, l, u, s, a, !1), Mt(e);
				return;
			case "select":
				for (a in Q("invalid", e), r = s = o = null, n) if (n.hasOwnProperty(a) && (c = n[a], c != null)) switch (a) {
					case "value":
						o = c;
						break;
					case "defaultValue":
						s = c;
						break;
					case "multiple": r = c;
					default: $(e, t, a, c, n, null);
				}
				t = o, n = s, e.multiple = !!r, t == null ? n != null && Bt(e, !!r, n, !0) : Bt(e, !!r, t, !1);
				return;
			case "textarea":
				for (s in Q("invalid", e), o = a = r = null, n) if (n.hasOwnProperty(s) && (c = n[s], c != null)) switch (s) {
					case "value":
						r = c;
						break;
					case "defaultValue":
						a = c;
						break;
					case "children":
						o = c;
						break;
					case "dangerouslySetInnerHTML":
						if (c != null) throw Error(i(91));
						break;
					default: $(e, t, s, c, n, null);
				}
				Ht(e, r, a, o), Mt(e);
				return;
			case "option":
				for (l in n) if (n.hasOwnProperty(l) && (r = n[l], r != null)) switch (l) {
					case "selected":
						e.selected = r && typeof r != "function" && typeof r != "symbol";
						break;
					default: $(e, t, l, r, n, null);
				}
				return;
			case "dialog":
				Q("beforetoggle", e), Q("toggle", e), Q("cancel", e), Q("close", e);
				break;
			case "iframe":
			case "object":
				Q("load", e);
				break;
			case "video":
			case "audio":
				for (r = 0; r < id.length; r++) Q(id[r], e);
				break;
			case "image":
				Q("error", e), Q("load", e);
				break;
			case "details":
				Q("toggle", e);
				break;
			case "embed":
			case "source":
			case "link": Q("error", e), Q("load", e);
			case "area":
			case "base":
			case "br":
			case "col":
			case "hr":
			case "keygen":
			case "meta":
			case "param":
			case "track":
			case "wbr":
			case "menuitem":
				for (u in n) if (n.hasOwnProperty(u) && (r = n[u], r != null)) switch (u) {
					case "children":
					case "dangerouslySetInnerHTML": throw Error(i(137, t));
					default: $(e, t, u, r, n, null);
				}
				return;
			default: if (qt(t)) {
				for (d in n) n.hasOwnProperty(d) && (r = n[d], r !== void 0 && xd(e, t, d, r, n, void 0));
				return;
			}
		}
		for (c in n) n.hasOwnProperty(c) && (r = n[c], r != null && $(e, t, c, r, n, null));
	}
	function Cd(e, t, n, r) {
		switch (t) {
			case "div":
			case "span":
			case "svg":
			case "path":
			case "a":
			case "g":
			case "p":
			case "li": break;
			case "input":
				var a = null, o = null, s = null, c = null, l = null, u = null, d = null;
				for (m in n) {
					var f = n[m];
					if (n.hasOwnProperty(m) && f != null) switch (m) {
						case "checked": break;
						case "value": break;
						case "defaultValue": l = f;
						default: r.hasOwnProperty(m) || $(e, t, m, null, r, f);
					}
				}
				for (var p in r) {
					var m = r[p];
					if (f = n[p], r.hasOwnProperty(p) && (m != null || f != null)) switch (p) {
						case "type":
							o = m;
							break;
						case "name":
							a = m;
							break;
						case "checked":
							u = m;
							break;
						case "defaultChecked":
							d = m;
							break;
						case "value":
							s = m;
							break;
						case "defaultValue":
							c = m;
							break;
						case "children":
						case "dangerouslySetInnerHTML":
							if (m != null) throw Error(i(137, t));
							break;
						default: m !== f && $(e, t, p, m, r, f);
					}
				}
				Lt(e, s, c, l, u, d, o, a);
				return;
			case "select":
				for (o in m = s = c = p = null, n) if (l = n[o], n.hasOwnProperty(o) && l != null) switch (o) {
					case "value": break;
					case "multiple": m = l;
					default: r.hasOwnProperty(o) || $(e, t, o, null, r, l);
				}
				for (a in r) if (o = r[a], l = n[a], r.hasOwnProperty(a) && (o != null || l != null)) switch (a) {
					case "value":
						p = o;
						break;
					case "defaultValue":
						c = o;
						break;
					case "multiple": s = o;
					default: o !== l && $(e, t, a, o, r, l);
				}
				t = c, n = s, r = m, p == null ? !!r != !!n && (t == null ? Bt(e, !!n, n ? [] : "", !1) : Bt(e, !!n, t, !0)) : Bt(e, !!n, p, !1);
				return;
			case "textarea":
				for (c in m = p = null, n) if (a = n[c], n.hasOwnProperty(c) && a != null && !r.hasOwnProperty(c)) switch (c) {
					case "value": break;
					case "children": break;
					default: $(e, t, c, null, r, a);
				}
				for (s in r) if (a = r[s], o = n[s], r.hasOwnProperty(s) && (a != null || o != null)) switch (s) {
					case "value":
						p = a;
						break;
					case "defaultValue":
						m = a;
						break;
					case "children": break;
					case "dangerouslySetInnerHTML":
						if (a != null) throw Error(i(91));
						break;
					default: a !== o && $(e, t, s, a, r, o);
				}
				Vt(e, p, m);
				return;
			case "option":
				for (var h in n) if (p = n[h], n.hasOwnProperty(h) && p != null && !r.hasOwnProperty(h)) switch (h) {
					case "selected":
						e.selected = !1;
						break;
					default: $(e, t, h, null, r, p);
				}
				for (l in r) if (p = r[l], m = n[l], r.hasOwnProperty(l) && p !== m && (p != null || m != null)) switch (l) {
					case "selected":
						e.selected = p && typeof p != "function" && typeof p != "symbol";
						break;
					default: $(e, t, l, p, r, m);
				}
				return;
			case "img":
			case "link":
			case "area":
			case "base":
			case "br":
			case "col":
			case "embed":
			case "hr":
			case "keygen":
			case "meta":
			case "param":
			case "source":
			case "track":
			case "wbr":
			case "menuitem":
				for (var g in n) p = n[g], n.hasOwnProperty(g) && p != null && !r.hasOwnProperty(g) && $(e, t, g, null, r, p);
				for (u in r) if (p = r[u], m = n[u], r.hasOwnProperty(u) && p !== m && (p != null || m != null)) switch (u) {
					case "children":
					case "dangerouslySetInnerHTML":
						if (p != null) throw Error(i(137, t));
						break;
					default: $(e, t, u, p, r, m);
				}
				return;
			default: if (qt(t)) {
				for (var _ in n) p = n[_], n.hasOwnProperty(_) && p !== void 0 && !r.hasOwnProperty(_) && xd(e, t, _, void 0, r, p);
				for (d in r) p = r[d], m = n[d], !r.hasOwnProperty(d) || p === m || p === void 0 && m === void 0 || xd(e, t, d, p, r, m);
				return;
			}
		}
		for (var v in n) p = n[v], n.hasOwnProperty(v) && p != null && !r.hasOwnProperty(v) && $(e, t, v, null, r, p);
		for (f in r) p = r[f], m = n[f], !r.hasOwnProperty(f) || p === m || p == null && m == null || $(e, t, f, p, r, m);
	}
	var wd = null, Td = null;
	function Ed(e) {
		return e.nodeType === 9 ? e : e.ownerDocument;
	}
	function Dd(e) {
		switch (e) {
			case "http://www.w3.org/2000/svg": return 1;
			case "http://www.w3.org/1998/Math/MathML": return 2;
			default: return 0;
		}
	}
	function Od(e, t) {
		if (e === 0) switch (t) {
			case "svg": return 1;
			case "math": return 2;
			default: return 0;
		}
		return e === 1 && t === "foreignObject" ? 0 : e;
	}
	function kd(e, t) {
		return e === "textarea" || e === "noscript" || typeof t.children == "string" || typeof t.children == "number" || typeof t.children == "bigint" || typeof t.dangerouslySetInnerHTML == "object" && t.dangerouslySetInnerHTML !== null && t.dangerouslySetInnerHTML.__html != null;
	}
	var Ad = null;
	function jd() {
		var e = window.event;
		return e && e.type === "popstate" ? e !== Ad && (Ad = e, !0) : (Ad = null, !1);
	}
	var Md = typeof setTimeout == "function" ? setTimeout : void 0, Nd = typeof clearTimeout == "function" ? clearTimeout : void 0, Pd = typeof Promise == "function" ? Promise : void 0, Fd = typeof queueMicrotask == "function" ? queueMicrotask : Pd === void 0 ? Md : function(e) {
		return Pd.resolve(null).then(e).catch(Id);
	};
	function Id(e) {
		setTimeout(function() {
			throw e;
		});
	}
	function Ld(e) {
		return e === "head";
	}
	function Rd(e, t) {
		var n = t, r = 0, i = 0;
		do {
			var a = n.nextSibling;
			if (e.removeChild(n), a && a.nodeType === 8) {
				if (n = a.data, n === "/$") {
					if (0 < r && 8 > r) {
						n = r;
						var o = e.ownerDocument;
						if (n & 1 && Jd(o.documentElement), n & 2 && Jd(o.body), n & 4) for (n = o.head, Jd(n), o = n.firstChild; o;) {
							var s = o.nextSibling, c = o.nodeName;
							o[it] || c === "SCRIPT" || c === "STYLE" || c === "LINK" && o.rel.toLowerCase() === "stylesheet" || n.removeChild(o), o = s;
						}
					}
					if (i === 0) {
						e.removeChild(a), hp(t);
						return;
					}
					i--;
				} else n === "$" || n === "$?" || n === "$!" ? i++ : r = n.charCodeAt(0) - 48;
			} else r = 0;
			n = a;
		} while (n);
		hp(t);
	}
	function zd(e) {
		var t = e.firstChild;
		for (t && t.nodeType === 10 && (t = t.nextSibling); t;) {
			var n = t;
			switch (t = t.nextSibling, n.nodeName) {
				case "HTML":
				case "HEAD":
				case "BODY":
					zd(n), at(n);
					continue;
				case "SCRIPT":
				case "STYLE": continue;
				case "LINK": if (n.rel.toLowerCase() === "stylesheet") continue;
			}
			e.removeChild(n);
		}
	}
	function Bd(e, t, n, r) {
		for (; e.nodeType === 1;) {
			var i = n;
			if (e.nodeName.toLowerCase() !== t.toLowerCase()) {
				if (!r && (e.nodeName !== "INPUT" || e.type !== "hidden")) break;
			} else if (!r) {
				if (t === "input" && e.type === "hidden") {
					var a = i.name == null ? null : "" + i.name;
					if (i.type === "hidden" && e.getAttribute("name") === a) return e;
				} else return e;
			} else if (!e[it]) switch (t) {
				case "meta":
					if (!e.hasAttribute("itemprop")) break;
					return e;
				case "link":
					if (a = e.getAttribute("rel"), a === "stylesheet" && e.hasAttribute("data-precedence") || a !== i.rel || e.getAttribute("href") !== (i.href == null || i.href === "" ? null : i.href) || e.getAttribute("crossorigin") !== (i.crossOrigin == null ? null : i.crossOrigin) || e.getAttribute("title") !== (i.title == null ? null : i.title)) break;
					return e;
				case "style":
					if (e.hasAttribute("data-precedence")) break;
					return e;
				case "script":
					if (a = e.getAttribute("src"), (a !== (i.src == null ? null : i.src) || e.getAttribute("type") !== (i.type == null ? null : i.type) || e.getAttribute("crossorigin") !== (i.crossOrigin == null ? null : i.crossOrigin)) && a && e.hasAttribute("async") && !e.hasAttribute("itemprop")) break;
					return e;
				default: return e;
			}
			if (e = Wd(e.nextSibling), e === null) break;
		}
		return null;
	}
	function Vd(e, t, n) {
		if (t === "") return null;
		for (; e.nodeType !== 3;) if ((e.nodeType !== 1 || e.nodeName !== "INPUT" || e.type !== "hidden") && !n || (e = Wd(e.nextSibling), e === null)) return null;
		return e;
	}
	function Hd(e) {
		return e.data === "$!" || e.data === "$?" && e.ownerDocument.readyState === "complete";
	}
	function Ud(e, t) {
		var n = e.ownerDocument;
		if (e.data !== "$?" || n.readyState === "complete") t();
		else {
			var r = function() {
				t(), n.removeEventListener("DOMContentLoaded", r);
			};
			n.addEventListener("DOMContentLoaded", r), e._reactRetry = r;
		}
	}
	function Wd(e) {
		for (; e != null; e = e.nextSibling) {
			var t = e.nodeType;
			if (t === 1 || t === 3) break;
			if (t === 8) {
				if (t = e.data, t === "$" || t === "$!" || t === "$?" || t === "F!" || t === "F") break;
				if (t === "/$") return null;
			}
		}
		return e;
	}
	var Gd = null;
	function Kd(e) {
		e = e.previousSibling;
		for (var t = 0; e;) {
			if (e.nodeType === 8) {
				var n = e.data;
				if (n === "$" || n === "$!" || n === "$?") {
					if (t === 0) return e;
					t--;
				} else n === "/$" && t++;
			}
			e = e.previousSibling;
		}
		return null;
	}
	function qd(e, t, n) {
		switch (t = Ed(n), e) {
			case "html":
				if (e = t.documentElement, !e) throw Error(i(452));
				return e;
			case "head":
				if (e = t.head, !e) throw Error(i(453));
				return e;
			case "body":
				if (e = t.body, !e) throw Error(i(454));
				return e;
			default: throw Error(i(451));
		}
	}
	function Jd(e) {
		for (var t = e.attributes; t.length;) e.removeAttributeNode(t[0]);
		at(e);
	}
	var Yd = /* @__PURE__ */ new Map(), Xd = /* @__PURE__ */ new Set();
	function Zd(e) {
		return typeof e.getRootNode == "function" ? e.getRootNode() : e.nodeType === 9 ? e : e.ownerDocument;
	}
	var Qd = F.d;
	F.d = {
		f: $d,
		r: ef,
		D: rf,
		C: af,
		L: of,
		m: sf,
		X: lf,
		S: cf,
		M: uf
	};
	function $d() {
		var e = Qd.f(), t = cu();
		return e || t;
	}
	function ef(e) {
		var t = st(e);
		t !== null && t.tag === 5 && t.type === "form" ? rs(t) : Qd.r(e);
	}
	var tf = typeof document > "u" ? null : document;
	function nf(e, t, n) {
		var r = tf;
		if (r && typeof t == "string" && t) {
			var i = It(t);
			i = "link[rel=\"" + e + "\"][href=\"" + i + "\"]", typeof n == "string" && (i += "[crossorigin=\"" + n + "\"]"), Xd.has(i) || (Xd.add(i), e = {
				rel: e,
				crossOrigin: n,
				href: t
			}, r.querySelector(i) === null && (t = r.createElement("link"), Sd(t, "link", e), ut(t), r.head.appendChild(t)));
		}
	}
	function rf(e) {
		Qd.D(e), nf("dns-prefetch", e, null);
	}
	function af(e, t) {
		Qd.C(e, t), nf("preconnect", e, t);
	}
	function of(e, t, n) {
		Qd.L(e, t, n);
		var r = tf;
		if (r && e && t) {
			var i = "link[rel=\"preload\"][as=\"" + It(t) + "\"]";
			t === "image" && n && n.imageSrcSet ? (i += "[imagesrcset=\"" + It(n.imageSrcSet) + "\"]", typeof n.imageSizes == "string" && (i += "[imagesizes=\"" + It(n.imageSizes) + "\"]")) : i += "[href=\"" + It(e) + "\"]";
			var a = i;
			switch (t) {
				case "style":
					a = ff(e);
					break;
				case "script": a = gf(e);
			}
			Yd.has(a) || (e = p({
				rel: "preload",
				href: t === "image" && n && n.imageSrcSet ? void 0 : e,
				as: t
			}, n), Yd.set(a, e), r.querySelector(i) !== null || t === "style" && r.querySelector(pf(a)) || t === "script" && r.querySelector(_f(a)) || (t = r.createElement("link"), Sd(t, "link", e), ut(t), r.head.appendChild(t)));
		}
	}
	function sf(e, t) {
		Qd.m(e, t);
		var n = tf;
		if (n && e) {
			var r = t && typeof t.as == "string" ? t.as : "script", i = "link[rel=\"modulepreload\"][as=\"" + It(r) + "\"][href=\"" + It(e) + "\"]", a = i;
			switch (r) {
				case "audioworklet":
				case "paintworklet":
				case "serviceworker":
				case "sharedworker":
				case "worker":
				case "script": a = gf(e);
			}
			if (!Yd.has(a) && (e = p({
				rel: "modulepreload",
				href: e
			}, t), Yd.set(a, e), n.querySelector(i) === null)) {
				switch (r) {
					case "audioworklet":
					case "paintworklet":
					case "serviceworker":
					case "sharedworker":
					case "worker":
					case "script": if (n.querySelector(_f(a))) return;
				}
				r = n.createElement("link"), Sd(r, "link", e), ut(r), n.head.appendChild(r);
			}
		}
	}
	function cf(e, t, n) {
		Qd.S(e, t, n);
		var r = tf;
		if (r && e) {
			var i = lt(r).hoistableStyles, a = ff(e);
			t ||= "default";
			var o = i.get(a);
			if (!o) {
				var s = {
					loading: 0,
					preload: null
				};
				if (o = r.querySelector(pf(a))) s.loading = 5;
				else {
					e = p({
						rel: "stylesheet",
						href: e,
						"data-precedence": t
					}, n), (n = Yd.get(a)) && bf(e, n);
					var c = o = r.createElement("link");
					ut(c), Sd(c, "link", e), c._p = new Promise(function(e, t) {
						c.onload = e, c.onerror = t;
					}), c.addEventListener("load", function() {
						s.loading |= 1;
					}), c.addEventListener("error", function() {
						s.loading |= 2;
					}), s.loading |= 4, yf(o, t, r);
				}
				o = {
					type: "stylesheet",
					instance: o,
					count: 1,
					state: s
				}, i.set(a, o);
			}
		}
	}
	function lf(e, t) {
		Qd.X(e, t);
		var n = tf;
		if (n && e) {
			var r = lt(n).hoistableScripts, i = gf(e), a = r.get(i);
			a || (a = n.querySelector(_f(i)), a || (e = p({
				src: e,
				async: !0
			}, t), (t = Yd.get(i)) && xf(e, t), a = n.createElement("script"), ut(a), Sd(a, "link", e), n.head.appendChild(a)), a = {
				type: "script",
				instance: a,
				count: 1,
				state: null
			}, r.set(i, a));
		}
	}
	function uf(e, t) {
		Qd.M(e, t);
		var n = tf;
		if (n && e) {
			var r = lt(n).hoistableScripts, i = gf(e), a = r.get(i);
			a || (a = n.querySelector(_f(i)), a || (e = p({
				src: e,
				async: !0,
				type: "module"
			}, t), (t = Yd.get(i)) && xf(e, t), a = n.createElement("script"), ut(a), Sd(a, "link", e), n.head.appendChild(a)), a = {
				type: "script",
				instance: a,
				count: 1,
				state: null
			}, r.set(i, a));
		}
	}
	function df(e, t, n, r) {
		var a = (a = oe.current) ? Zd(a) : null;
		if (!a) throw Error(i(446));
		switch (e) {
			case "meta":
			case "title": return null;
			case "style": return typeof n.precedence == "string" && typeof n.href == "string" ? (t = ff(n.href), n = lt(a).hoistableStyles, r = n.get(t), r || (r = {
				type: "style",
				instance: null,
				count: 0,
				state: null
			}, n.set(t, r)), r) : {
				type: "void",
				instance: null,
				count: 0,
				state: null
			};
			case "link":
				if (n.rel === "stylesheet" && typeof n.href == "string" && typeof n.precedence == "string") {
					e = ff(n.href);
					var o = lt(a).hoistableStyles, s = o.get(e);
					if (s || (a = a.ownerDocument || a, s = {
						type: "stylesheet",
						instance: null,
						count: 0,
						state: {
							loading: 0,
							preload: null
						}
					}, o.set(e, s), (o = a.querySelector(pf(e))) && !o._p && (s.instance = o, s.state.loading = 5), Yd.has(e) || (n = {
						rel: "preload",
						as: "style",
						href: n.href,
						crossOrigin: n.crossOrigin,
						integrity: n.integrity,
						media: n.media,
						hrefLang: n.hrefLang,
						referrerPolicy: n.referrerPolicy
					}, Yd.set(e, n), o || hf(a, e, n, s.state))), t && r === null) throw Error(i(528, ""));
					return s;
				}
				if (t && r !== null) throw Error(i(529, ""));
				return null;
			case "script": return t = n.async, n = n.src, typeof n == "string" && t && typeof t != "function" && typeof t != "symbol" ? (t = gf(n), n = lt(a).hoistableScripts, r = n.get(t), r || (r = {
				type: "script",
				instance: null,
				count: 0,
				state: null
			}, n.set(t, r)), r) : {
				type: "void",
				instance: null,
				count: 0,
				state: null
			};
			default: throw Error(i(444, e));
		}
	}
	function ff(e) {
		return "href=\"" + It(e) + "\"";
	}
	function pf(e) {
		return "link[rel=\"stylesheet\"][" + e + "]";
	}
	function mf(e) {
		return p({}, e, {
			"data-precedence": e.precedence,
			precedence: null
		});
	}
	function hf(e, t, n, r) {
		e.querySelector("link[rel=\"preload\"][as=\"style\"][" + t + "]") ? r.loading = 1 : (t = e.createElement("link"), r.preload = t, t.addEventListener("load", function() {
			return r.loading |= 1;
		}), t.addEventListener("error", function() {
			return r.loading |= 2;
		}), Sd(t, "link", n), ut(t), e.head.appendChild(t));
	}
	function gf(e) {
		return "[src=\"" + It(e) + "\"]";
	}
	function _f(e) {
		return "script[async]" + e;
	}
	function vf(e, t, n) {
		if (t.count++, t.instance === null) switch (t.type) {
			case "style":
				var r = e.querySelector("style[data-href~=\"" + It(n.href) + "\"]");
				if (r) return t.instance = r, ut(r), r;
				var a = p({}, n, {
					"data-href": n.href,
					"data-precedence": n.precedence,
					href: null,
					precedence: null
				});
				return r = (e.ownerDocument || e).createElement("style"), ut(r), Sd(r, "style", a), yf(r, n.precedence, e), t.instance = r;
			case "stylesheet":
				a = ff(n.href);
				var o = e.querySelector(pf(a));
				if (o) return t.state.loading |= 4, t.instance = o, ut(o), o;
				r = mf(n), (a = Yd.get(a)) && bf(r, a), o = (e.ownerDocument || e).createElement("link"), ut(o);
				var s = o;
				return s._p = new Promise(function(e, t) {
					s.onload = e, s.onerror = t;
				}), Sd(o, "link", r), t.state.loading |= 4, yf(o, n.precedence, e), t.instance = o;
			case "script": return o = gf(n.src), (a = e.querySelector(_f(o))) ? (t.instance = a, ut(a), a) : (r = n, (a = Yd.get(o)) && (r = p({}, n), xf(r, a)), e = e.ownerDocument || e, a = e.createElement("script"), ut(a), Sd(a, "link", r), e.head.appendChild(a), t.instance = a);
			case "void": return null;
			default: throw Error(i(443, t.type));
		}
		else t.type === "stylesheet" && !(t.state.loading & 4) && (r = t.instance, t.state.loading |= 4, yf(r, n.precedence, e));
		return t.instance;
	}
	function yf(e, t, n) {
		for (var r = n.querySelectorAll("link[rel=\"stylesheet\"][data-precedence],style[data-precedence]"), i = r.length ? r[r.length - 1] : null, a = i, o = 0; o < r.length; o++) {
			var s = r[o];
			if (s.dataset.precedence === t) a = s;
			else if (a !== i) break;
		}
		a ? a.parentNode.insertBefore(e, a.nextSibling) : (t = n.nodeType === 9 ? n.head : n, t.insertBefore(e, t.firstChild));
	}
	function bf(e, t) {
		e.crossOrigin ??= t.crossOrigin, e.referrerPolicy ??= t.referrerPolicy, e.title ??= t.title;
	}
	function xf(e, t) {
		e.crossOrigin ??= t.crossOrigin, e.referrerPolicy ??= t.referrerPolicy, e.integrity ??= t.integrity;
	}
	var Sf = null;
	function Cf(e, t, n) {
		if (Sf === null) {
			var r = /* @__PURE__ */ new Map(), i = Sf = /* @__PURE__ */ new Map();
			i.set(n, r);
		} else i = Sf, r = i.get(n), r || (r = /* @__PURE__ */ new Map(), i.set(n, r));
		if (r.has(e)) return r;
		for (r.set(e, null), n = n.getElementsByTagName(e), i = 0; i < n.length; i++) {
			var a = n[i];
			if (!(a[it] || a[Ze] || e === "link" && a.getAttribute("rel") === "stylesheet") && a.namespaceURI !== "http://www.w3.org/2000/svg") {
				var o = a.getAttribute(t) || "";
				o = e + o;
				var s = r.get(o);
				s ? s.push(a) : r.set(o, [a]);
			}
		}
		return r;
	}
	function wf(e, t, n) {
		e = e.ownerDocument || e, e.head.insertBefore(n, t === "title" ? e.querySelector("head > title") : null);
	}
	function Tf(e, t, n) {
		if (n === 1 || t.itemProp != null) return !1;
		switch (e) {
			case "meta":
			case "title": return !0;
			case "style":
				if (typeof t.precedence != "string" || typeof t.href != "string" || t.href === "") break;
				return !0;
			case "link":
				if (typeof t.rel != "string" || typeof t.href != "string" || t.href === "" || t.onLoad || t.onError) break;
				switch (t.rel) {
					case "stylesheet": return e = t.disabled, typeof t.precedence == "string" && e == null;
					default: return !0;
				}
			case "script": if (t.async && typeof t.async != "function" && typeof t.async != "symbol" && !t.onLoad && !t.onError && t.src && typeof t.src == "string") return !0;
		}
		return !1;
	}
	function Ef(e) {
		return !(e.type === "stylesheet" && !(e.state.loading & 3));
	}
	var Df = null;
	function Of() {}
	function kf(e, t, n) {
		if (Df === null) throw Error(i(475));
		var r = Df;
		if (t.type === "stylesheet" && (typeof n.media != "string" || !1 !== matchMedia(n.media).matches) && !(t.state.loading & 4)) {
			if (t.instance === null) {
				var a = ff(n.href), o = e.querySelector(pf(a));
				if (o) {
					e = o._p, typeof e == "object" && e && typeof e.then == "function" && (r.count++, r = jf.bind(r), e.then(r, r)), t.state.loading |= 4, t.instance = o, ut(o);
					return;
				}
				o = e.ownerDocument || e, n = mf(n), (a = Yd.get(a)) && bf(n, a), o = o.createElement("link"), ut(o);
				var s = o;
				s._p = new Promise(function(e, t) {
					s.onload = e, s.onerror = t;
				}), Sd(o, "link", n), t.instance = o;
			}
			r.stylesheets === null && (r.stylesheets = /* @__PURE__ */ new Map()), r.stylesheets.set(t, e), (e = t.state.preload) && !(t.state.loading & 3) && (r.count++, t = jf.bind(r), e.addEventListener("load", t), e.addEventListener("error", t));
		}
	}
	function Af() {
		if (Df === null) throw Error(i(475));
		var e = Df;
		return e.stylesheets && e.count === 0 && Nf(e, e.stylesheets), 0 < e.count ? function(t) {
			var n = setTimeout(function() {
				if (e.stylesheets && Nf(e, e.stylesheets), e.unsuspend) {
					var t = e.unsuspend;
					e.unsuspend = null, t();
				}
			}, 6e4);
			return e.unsuspend = t, function() {
				e.unsuspend = null, clearTimeout(n);
			};
		} : null;
	}
	function jf() {
		if (this.count--, this.count === 0) {
			if (this.stylesheets) Nf(this, this.stylesheets);
			else if (this.unsuspend) {
				var e = this.unsuspend;
				this.unsuspend = null, e();
			}
		}
	}
	var Mf = null;
	function Nf(e, t) {
		e.stylesheets = null, e.unsuspend !== null && (e.count++, Mf = /* @__PURE__ */ new Map(), t.forEach(Pf, e), Mf = null, jf.call(e));
	}
	function Pf(e, t) {
		if (!(t.state.loading & 4)) {
			var n = Mf.get(e);
			if (n) var r = n.get(null);
			else {
				n = /* @__PURE__ */ new Map(), Mf.set(e, n);
				for (var i = e.querySelectorAll("link[data-precedence],style[data-precedence]"), a = 0; a < i.length; a++) {
					var o = i[a];
					(o.nodeName === "LINK" || o.getAttribute("media") !== "not all") && (n.set(o.dataset.precedence, o), r = o);
				}
				r && n.set(null, r);
			}
			i = t.instance, o = i.getAttribute("data-precedence"), a = n.get(o) || r, a === r && n.set(null, i), n.set(o, i), this.count++, r = jf.bind(this), i.addEventListener("load", r), i.addEventListener("error", r), a ? a.parentNode.insertBefore(i, a.nextSibling) : (e = e.nodeType === 9 ? e.head : e, e.insertBefore(i, e.firstChild)), t.state.loading |= 4;
		}
	}
	var Ff = {
		$$typeof: C,
		Provider: null,
		Consumer: null,
		_currentValue: ne,
		_currentValue2: ne,
		_threadCount: 0
	};
	function If(e, t, n, r, i, a, o, s) {
		this.tag = 1, this.containerInfo = e, this.pingCache = this.current = this.pendingChildren = null, this.timeoutHandle = -1, this.callbackNode = this.next = this.pendingContext = this.context = this.cancelPendingCommit = null, this.callbackPriority = 0, this.expirationTimes = Ve(-1), this.entangledLanes = this.shellSuspendCounter = this.errorRecoveryDisabledLanes = this.expiredLanes = this.warmLanes = this.pingedLanes = this.suspendedLanes = this.pendingLanes = 0, this.entanglements = Ve(0), this.hiddenUpdates = Ve(null), this.identifierPrefix = r, this.onUncaughtError = i, this.onCaughtError = a, this.onRecoverableError = o, this.pooledCache = null, this.pooledCacheLanes = 0, this.formState = s, this.incompleteTransitions = /* @__PURE__ */ new Map();
	}
	function Lf(e, t, n, r, i, a, o, s, c, l, u, d) {
		return e = new If(e, t, n, o, s, c, l, d), t = 1, !0 === a && (t |= 24), a = oi(3, null, null, t), e.current = a, a.stateNode = e, t = na(), t.refCount++, e.pooledCache = t, t.refCount++, a.memoizedState = {
			element: r,
			isDehydrated: n,
			cache: t
		}, Da(a), e;
	}
	function Rf(e) {
		return e ? (e = ii, e) : ii;
	}
	function zf(e, t, n, r, i, a) {
		i = Rf(i), r.context === null ? r.context = i : r.pendingContext = i, r = V(t), r.payload = { element: n }, a = a === void 0 ? null : a, a !== null && (r.callback = a), n = ka(e, r, t), n !== null && (ru(n, e, t), Aa(n, e, t));
	}
	function Bf(e, t) {
		if (e = e.memoizedState, e !== null && e.dehydrated !== null) {
			var n = e.retryLane;
			e.retryLane = n !== 0 && n < t ? n : t;
		}
	}
	function Vf(e, t) {
		Bf(e, t), (e = e.alternate) && Bf(e, t);
	}
	function Hf(e) {
		if (e.tag === 13) {
			var t = ti(e, 67108864);
			t !== null && ru(t, e, 67108864), Vf(e, 67108864);
		}
	}
	var Uf = !0;
	function Wf(e, t, n, r) {
		var i = P.T;
		P.T = null;
		var a = F.p;
		try {
			F.p = 2, Kf(e, t, n, r);
		} finally {
			F.p = a, P.T = i;
		}
	}
	function Gf(e, t, n, r) {
		var i = P.T;
		P.T = null;
		var a = F.p;
		try {
			F.p = 8, Kf(e, t, n, r);
		} finally {
			F.p = a, P.T = i;
		}
	}
	function Kf(e, t, n, r) {
		if (Uf) {
			var i = qf(r);
			if (i === null) dd(e, t, r, Jf, n), ap(e, r);
			else if (sp(i, e, t, n, r)) r.stopPropagation();
			else if (ap(e, r), t & 4 && -1 < ip.indexOf(e)) {
				for (; i !== null;) {
					var a = st(i);
					if (a !== null) switch (a.tag) {
						case 3:
							if (a = a.stateNode, a.current.memoizedState.isDehydrated) {
								var o = Fe(a.pendingLanes);
								if (o !== 0) {
									var s = a;
									for (s.pendingLanes |= 2, s.entangledLanes |= 2; o;) {
										var c = 1 << 31 - ke(o);
										s.entanglements[1] |= c, o &= ~c;
									}
									Wu(a), !(K & 6) && (Ul = _e() + 500, Gu(0, !1));
								}
							}
							break;
						case 13: s = ti(a, 2), s !== null && ru(s, a, 2), cu(), Vf(a, 2);
					}
					if (a = qf(r), a === null && dd(e, t, r, Jf, n), a === i) break;
					i = a;
				}
				i !== null && r.stopPropagation();
			} else dd(e, t, r, null, n);
		}
	}
	function qf(e) {
		return e = Qt(e), Yf(e);
	}
	var Jf = null;
	function Yf(e) {
		if (Jf = null, e = ot(e), e !== null) {
			var t = o(e);
			if (t === null) e = null;
			else {
				var n = t.tag;
				if (n === 13) {
					if (e = s(t), e !== null) return e;
					e = null;
				} else if (n === 3) {
					if (t.stateNode.current.memoizedState.isDehydrated) return t.tag === 3 ? t.stateNode.containerInfo : null;
					e = null;
				} else t !== e && (e = null);
			}
		}
		return Jf = e, null;
	}
	function Xf(e) {
		switch (e) {
			case "beforetoggle":
			case "cancel":
			case "click":
			case "close":
			case "contextmenu":
			case "copy":
			case "cut":
			case "auxclick":
			case "dblclick":
			case "dragend":
			case "dragstart":
			case "drop":
			case "focusin":
			case "focusout":
			case "input":
			case "invalid":
			case "keydown":
			case "keypress":
			case "keyup":
			case "mousedown":
			case "mouseup":
			case "paste":
			case "pause":
			case "play":
			case "pointercancel":
			case "pointerdown":
			case "pointerup":
			case "ratechange":
			case "reset":
			case "resize":
			case "seeked":
			case "submit":
			case "toggle":
			case "touchcancel":
			case "touchend":
			case "touchstart":
			case "volumechange":
			case "change":
			case "selectionchange":
			case "textInput":
			case "compositionstart":
			case "compositionend":
			case "compositionupdate":
			case "beforeblur":
			case "afterblur":
			case "beforeinput":
			case "blur":
			case "fullscreenchange":
			case "focus":
			case "hashchange":
			case "popstate":
			case "select":
			case "selectstart": return 2;
			case "drag":
			case "dragenter":
			case "dragexit":
			case "dragleave":
			case "dragover":
			case "mousemove":
			case "mouseout":
			case "mouseover":
			case "pointermove":
			case "pointerout":
			case "pointerover":
			case "scroll":
			case "touchmove":
			case "wheel":
			case "mouseenter":
			case "mouseleave":
			case "pointerenter":
			case "pointerleave": return 8;
			case "message": switch (ve()) {
				case ye: return 2;
				case be: return 8;
				case xe:
				case Se: return 32;
				case Ce: return 268435456;
				default: return 32;
			}
			default: return 32;
		}
	}
	var Zf = !1, Qf = null, $f = null, ep = null, tp = /* @__PURE__ */ new Map(), np = /* @__PURE__ */ new Map(), rp = [], ip = "mousedown mouseup touchcancel touchend touchstart auxclick dblclick pointercancel pointerdown pointerup dragend dragstart drop compositionend compositionstart keydown keypress keyup input textInput copy cut paste click change contextmenu reset".split(" ");
	function ap(e, t) {
		switch (e) {
			case "focusin":
			case "focusout":
				Qf = null;
				break;
			case "dragenter":
			case "dragleave":
				$f = null;
				break;
			case "mouseover":
			case "mouseout":
				ep = null;
				break;
			case "pointerover":
			case "pointerout":
				tp.delete(t.pointerId);
				break;
			case "gotpointercapture":
			case "lostpointercapture": np.delete(t.pointerId);
		}
	}
	function op(e, t, n, r, i, a) {
		return e === null || e.nativeEvent !== a ? (e = {
			blockedOn: t,
			domEventName: n,
			eventSystemFlags: r,
			nativeEvent: a,
			targetContainers: [i]
		}, t !== null && (t = st(t), t !== null && Hf(t)), e) : (e.eventSystemFlags |= r, t = e.targetContainers, i !== null && t.indexOf(i) === -1 && t.push(i), e);
	}
	function sp(e, t, n, r, i) {
		switch (t) {
			case "focusin": return Qf = op(Qf, e, t, n, r, i), !0;
			case "dragenter": return $f = op($f, e, t, n, r, i), !0;
			case "mouseover": return ep = op(ep, e, t, n, r, i), !0;
			case "pointerover":
				var a = i.pointerId;
				return tp.set(a, op(tp.get(a) || null, e, t, n, r, i)), !0;
			case "gotpointercapture": return a = i.pointerId, np.set(a, op(np.get(a) || null, e, t, n, r, i)), !0;
		}
		return !1;
	}
	function cp(e) {
		var t = ot(e.target);
		if (t !== null) {
			var n = o(t);
			if (n !== null) {
				if (t = n.tag, t === 13) {
					if (t = s(n), t !== null) {
						e.blockedOn = t, Ye(e.priority, function() {
							if (n.tag === 13) {
								var e = tu();
								e = Ke(e);
								var t = ti(n, e);
								t !== null && ru(t, n, e), Vf(n, e);
							}
						});
						return;
					}
				} else if (t === 3 && n.stateNode.current.memoizedState.isDehydrated) {
					e.blockedOn = n.tag === 3 ? n.stateNode.containerInfo : null;
					return;
				}
			}
		}
		e.blockedOn = null;
	}
	function lp(e) {
		if (e.blockedOn !== null) return !1;
		for (var t = e.targetContainers; 0 < t.length;) {
			var n = qf(e.nativeEvent);
			if (n === null) {
				n = e.nativeEvent;
				var r = new n.constructor(n.type, n);
				Zt = r, n.target.dispatchEvent(r), Zt = null;
			} else return t = st(n), t !== null && Hf(t), e.blockedOn = n, !1;
			t.shift();
		}
		return !0;
	}
	function up(e, t, n) {
		lp(e) && n.delete(t);
	}
	function dp() {
		Zf = !1, Qf !== null && lp(Qf) && (Qf = null), $f !== null && lp($f) && ($f = null), ep !== null && lp(ep) && (ep = null), tp.forEach(up), np.forEach(up);
	}
	function fp(e, n) {
		e.blockedOn === n && (e.blockedOn = null, Zf || (Zf = !0, t.unstable_scheduleCallback(t.unstable_NormalPriority, dp)));
	}
	var pp = null;
	function mp(e) {
		pp !== e && (pp = e, t.unstable_scheduleCallback(t.unstable_NormalPriority, function() {
			pp === e && (pp = null);
			for (var t = 0; t < e.length; t += 3) {
				var n = e[t], r = e[t + 1], i = e[t + 2];
				if (typeof r != "function") {
					if (Yf(r || n) === null) continue;
					break;
				}
				var a = st(n);
				a !== null && (e.splice(t, 3), t -= 3, ts(a, {
					pending: !0,
					data: i,
					method: n.method,
					action: r
				}, r, i));
			}
		}));
	}
	function hp(e) {
		function t(t) {
			return fp(t, e);
		}
		Qf !== null && fp(Qf, e), $f !== null && fp($f, e), ep !== null && fp(ep, e), tp.forEach(t), np.forEach(t);
		for (var n = 0; n < rp.length; n++) {
			var r = rp[n];
			r.blockedOn === e && (r.blockedOn = null);
		}
		for (; 0 < rp.length && (n = rp[0], n.blockedOn === null);) cp(n), n.blockedOn === null && rp.shift();
		if (n = (e.ownerDocument || e).$$reactFormReplay, n != null) for (r = 0; r < n.length; r += 3) {
			var i = n[r], a = n[r + 1], o = i[Qe] || null;
			if (typeof a == "function") o || mp(n);
			else if (o) {
				var s = null;
				if (a && a.hasAttribute("formAction")) {
					if (i = a, o = a[Qe] || null) s = o.formAction;
					else if (Yf(i) !== null) continue;
				} else s = o.action;
				typeof s == "function" ? n[r + 1] = s : (n.splice(r, 3), r -= 3), mp(n);
			}
		}
	}
	function gp(e) {
		this._internalRoot = e;
	}
	_p.prototype.render = gp.prototype.render = function(e) {
		var t = this._internalRoot;
		if (t === null) throw Error(i(409));
		var n = t.current;
		zf(n, tu(), e, t, null, null);
	}, _p.prototype.unmount = gp.prototype.unmount = function() {
		var e = this._internalRoot;
		if (e !== null) {
			this._internalRoot = null;
			var t = e.containerInfo;
			zf(e.current, 2, null, e, null, null), cu(), t[$e] = null;
		}
	};
	function _p(e) {
		this._internalRoot = e;
	}
	_p.prototype.unstable_scheduleHydration = function(e) {
		if (e) {
			var t = Je();
			e = {
				blockedOn: null,
				target: e,
				priority: t
			};
			for (var n = 0; n < rp.length && t !== 0 && t < rp[n].priority; n++);
			rp.splice(n, 0, e), n === 0 && cp(e);
		}
	};
	var vp = n.version;
	if (vp !== "19.1.0") throw Error(i(527, vp, "19.1.0"));
	F.findDOMNode = function(e) {
		var t = e._reactInternals;
		if (t === void 0) throw typeof e.render == "function" ? Error(i(188)) : (e = Object.keys(e).join(","), Error(i(268, e)));
		return e = l(t), e = e === null ? null : d(e), e = e === null ? null : e.stateNode, e;
	};
	var yp = {
		bundleType: 0,
		version: "19.1.0",
		rendererPackageName: "react-dom",
		currentDispatcherRef: P,
		reconcilerVersion: "19.1.0"
	};
	if (typeof __REACT_DEVTOOLS_GLOBAL_HOOK__ < "u") {
		var bp = __REACT_DEVTOOLS_GLOBAL_HOOK__;
		if (!bp.isDisabled && bp.supportsFiber) try {
			Ee = bp.inject(yp), De = bp;
		} catch {}
	}
	e.createRoot = function(e, t) {
		if (!a(e)) throw Error(i(299));
		var n = !1, r = "", o = Hs, s = Us, c = Ws, l = null;
		return t != null && (!0 === t.unstable_strictMode && (n = !0), t.identifierPrefix !== void 0 && (r = t.identifierPrefix), t.onUncaughtError !== void 0 && (o = t.onUncaughtError), t.onCaughtError !== void 0 && (s = t.onCaughtError), t.onRecoverableError !== void 0 && (c = t.onRecoverableError), t.unstable_transitionCallbacks !== void 0 && (l = t.unstable_transitionCallbacks)), t = Lf(e, 1, !1, null, null, n, r, o, s, c, l, null), e[$e] = t.current, ld(e), new gp(t);
	};
})), g = /* @__PURE__ */ o(((e, t) => {
	function n() {
		if (typeof __REACT_DEVTOOLS_GLOBAL_HOOK__ < "u" && typeof __REACT_DEVTOOLS_GLOBAL_HOOK__.checkDCE == "function") try {
			__REACT_DEVTOOLS_GLOBAL_HOOK__.checkDCE(n);
		} catch (e) {
			console.error(e);
		}
	}
	n(), t.exports = h();
})), _ = /* @__PURE__ */ o(((e) => {
	var t = Symbol.for("react.transitional.element"), n = Symbol.for("react.fragment");
	function r(e, n, r) {
		var i = null;
		if (r !== void 0 && (i = "" + r), n.key !== void 0 && (i = "" + n.key), "key" in n) for (var a in r = {}, n) a !== "key" && (r[a] = n[a]);
		else r = n;
		return n = r.ref, {
			$$typeof: t,
			type: e,
			key: i,
			ref: n === void 0 ? null : n,
			props: r
		};
	}
	e.Fragment = n, e.jsx = r, e.jsxs = r;
})), v = /* @__PURE__ */ o(((e, t) => {
	t.exports = _();
})), y = /* @__PURE__ */ c(u(), 1), b = g(), x = v();
function S(e) {
	if (typeof e == "string" || typeof e == "number") return "" + e;
	let t = "";
	if (Array.isArray(e)) for (let n = 0, r; n < e.length; n++) (r = S(e[n])) !== "" && (t += (t && " ") + r);
	else for (let n in e) e[n] && (t += (t && " ") + n);
	return t;
}
// node_modules/d3-dispatch/src/dispatch.js
var C = { value: () => {} };
function w() {
	for (var e = 0, t = arguments.length, n = {}, r; e < t; ++e) {
		if (!(r = arguments[e] + "") || r in n || /[\s.]/.test(r)) throw Error("illegal type: " + r);
		n[r] = [];
	}
	return new T(n);
}
function T(e) {
	this._ = e;
}
function E(e, t) {
	return e.trim().split(/^|\s+/).map(function(e) {
		var n = "", r = e.indexOf(".");
		if (r >= 0 && (n = e.slice(r + 1), e = e.slice(0, r)), e && !t.hasOwnProperty(e)) throw Error("unknown type: " + e);
		return {
			type: e,
			name: n
		};
	});
}
T.prototype = w.prototype = {
	constructor: T,
	on: function(e, t) {
		var n = this._, r = E(e + "", n), i, a = -1, o = r.length;
		if (arguments.length < 2) {
			for (; ++a < o;) if ((i = (e = r[a]).type) && (i = D(n[i], e.name))) return i;
		} else {
			if (t != null && typeof t != "function") throw Error("invalid callback: " + t);
			for (; ++a < o;) if (i = (e = r[a]).type) n[i] = O(n[i], e.name, t);
			else if (t == null) for (i in n) n[i] = O(n[i], e.name, null);
			return this;
		}
	},
	copy: function() {
		var e = {}, t = this._;
		for (var n in t) e[n] = t[n].slice();
		return new T(e);
	},
	call: function(e, t) {
		if ((i = arguments.length - 2) > 0) for (var n = Array(i), r = 0, i, a; r < i; ++r) n[r] = arguments[r + 2];
		if (!this._.hasOwnProperty(e)) throw Error("unknown type: " + e);
		for (a = this._[e], r = 0, i = a.length; r < i; ++r) a[r].value.apply(t, n);
	},
	apply: function(e, t, n) {
		if (!this._.hasOwnProperty(e)) throw Error("unknown type: " + e);
		for (var r = this._[e], i = 0, a = r.length; i < a; ++i) r[i].value.apply(t, n);
	}
};
function D(e, t) {
	for (var n = 0, r = e.length, i; n < r; ++n) if ((i = e[n]).name === t) return i.value;
}
function O(e, t, n) {
	for (var r = 0, i = e.length; r < i; ++r) if (e[r].name === t) {
		e[r] = C, e = e.slice(0, r).concat(e.slice(r + 1));
		break;
	}
	return n != null && e.push({
		name: t,
		value: n
	}), e;
}
var k = {
	svg: "http://www.w3.org/2000/svg",
	xhtml: "http://www.w3.org/1999/xhtml",
	xlink: "http://www.w3.org/1999/xlink",
	xml: "http://www.w3.org/XML/1998/namespace",
	xmlns: "http://www.w3.org/2000/xmlns/"
};
// node_modules/d3-selection/src/namespace.js
function A(e) {
	var t = e += "", n = t.indexOf(":");
	return n >= 0 && (t = e.slice(0, n)) !== "xmlns" && (e = e.slice(n + 1)), k.hasOwnProperty(t) ? {
		space: k[t],
		local: e
	} : e;
}
// node_modules/d3-selection/src/creator.js
function j(e) {
	return function() {
		var t = this.ownerDocument, n = this.namespaceURI;
		return n === "http://www.w3.org/1999/xhtml" && t.documentElement.namespaceURI === "http://www.w3.org/1999/xhtml" ? t.createElement(e) : t.createElementNS(n, e);
	};
}
function M(e) {
	return function() {
		return this.ownerDocument.createElementNS(e.space, e.local);
	};
}
function ee(e) {
	var t = A(e);
	return (t.local ? M : j)(t);
}
// node_modules/d3-selection/src/selector.js
function N() {}
function te(e) {
	return e == null ? N : function() {
		return this.querySelector(e);
	};
}
// node_modules/d3-selection/src/selection/select.js
function P(e) {
	typeof e != "function" && (e = te(e));
	for (var t = this._groups, n = t.length, r = Array(n), i = 0; i < n; ++i) for (var a = t[i], o = a.length, s = r[i] = Array(o), c, l, u = 0; u < o; ++u) (c = a[u]) && (l = e.call(c, c.__data__, u, a)) && ("__data__" in c && (l.__data__ = c.__data__), s[u] = l);
	return new Rt(r, this._parents);
}
// node_modules/d3-selection/src/array.js
function F(e) {
	return e == null ? [] : Array.isArray(e) ? e : Array.from(e);
}
// node_modules/d3-selection/src/selectorAll.js
function ne() {
	return [];
}
function re(e) {
	return e == null ? ne : function() {
		return this.querySelectorAll(e);
	};
}
// node_modules/d3-selection/src/selection/selectAll.js
function I(e) {
	return function() {
		return F(e.apply(this, arguments));
	};
}
function L(e) {
	e = typeof e == "function" ? I(e) : re(e);
	for (var t = this._groups, n = t.length, r = [], i = [], a = 0; a < n; ++a) for (var o = t[a], s = o.length, c, l = 0; l < s; ++l) (c = o[l]) && (r.push(e.call(c, c.__data__, l, o)), i.push(c));
	return new Rt(r, i);
}
// node_modules/d3-selection/src/matcher.js
function R(e) {
	return function() {
		return this.matches(e);
	};
}
function z(e) {
	return function(t) {
		return t.matches(e);
	};
}
// node_modules/d3-selection/src/selection/selectChild.js
var ie = Array.prototype.find;
function ae(e) {
	return function() {
		return ie.call(this.children, e);
	};
}
function oe() {
	return this.firstElementChild;
}
function se(e) {
	return this.select(e == null ? oe : ae(typeof e == "function" ? e : z(e)));
}
// node_modules/d3-selection/src/selection/selectChildren.js
var ce = Array.prototype.filter;
function le() {
	return Array.from(this.children);
}
function ue(e) {
	return function() {
		return ce.call(this.children, e);
	};
}
function de(e) {
	return this.selectAll(e == null ? le : ue(typeof e == "function" ? e : z(e)));
}
// node_modules/d3-selection/src/selection/filter.js
function fe(e) {
	typeof e != "function" && (e = R(e));
	for (var t = this._groups, n = t.length, r = Array(n), i = 0; i < n; ++i) for (var a = t[i], o = a.length, s = r[i] = [], c, l = 0; l < o; ++l) (c = a[l]) && e.call(c, c.__data__, l, a) && s.push(c);
	return new Rt(r, this._parents);
}
// node_modules/d3-selection/src/selection/sparse.js
function pe(e) {
	return Array(e.length);
}
// node_modules/d3-selection/src/selection/enter.js
function me() {
	return new Rt(this._enter || this._groups.map(pe), this._parents);
}
function he(e, t) {
	this.ownerDocument = e.ownerDocument, this.namespaceURI = e.namespaceURI, this._next = null, this._parent = e, this.__data__ = t;
}
he.prototype = {
	constructor: he,
	appendChild: function(e) {
		return this._parent.insertBefore(e, this._next);
	},
	insertBefore: function(e, t) {
		return this._parent.insertBefore(e, t);
	},
	querySelector: function(e) {
		return this._parent.querySelector(e);
	},
	querySelectorAll: function(e) {
		return this._parent.querySelectorAll(e);
	}
};
// node_modules/d3-selection/src/constant.js
function ge(e) {
	return function() {
		return e;
	};
}
// node_modules/d3-selection/src/selection/data.js
function _e(e, t, n, r, i, a) {
	for (var o = 0, s, c = t.length, l = a.length; o < l; ++o) (s = t[o]) ? (s.__data__ = a[o], r[o] = s) : n[o] = new he(e, a[o]);
	for (; o < c; ++o) (s = t[o]) && (i[o] = s);
}
function ve(e, t, n, r, i, a, o) {
	var s, c, l = /* @__PURE__ */ new Map(), u = t.length, d = a.length, f = Array(u), p;
	for (s = 0; s < u; ++s) (c = t[s]) && (f[s] = p = o.call(c, c.__data__, s, t) + "", l.has(p) ? i[s] = c : l.set(p, c));
	for (s = 0; s < d; ++s) p = o.call(e, a[s], s, a) + "", (c = l.get(p)) ? (r[s] = c, c.__data__ = a[s], l.delete(p)) : n[s] = new he(e, a[s]);
	for (s = 0; s < u; ++s) (c = t[s]) && l.get(f[s]) === c && (i[s] = c);
}
function ye(e) {
	return e.__data__;
}
function be(e, t) {
	if (!arguments.length) return Array.from(this, ye);
	var n = t ? ve : _e, r = this._parents, i = this._groups;
	typeof e != "function" && (e = ge(e));
	for (var a = i.length, o = Array(a), s = Array(a), c = Array(a), l = 0; l < a; ++l) {
		var u = r[l], d = i[l], f = d.length, p = xe(e.call(u, u && u.__data__, l, r)), m = p.length, h = s[l] = Array(m), g = o[l] = Array(m);
		n(u, d, h, g, c[l] = Array(f), p, t);
		for (var _ = 0, v = 0, y, b; _ < m; ++_) if (y = h[_]) {
			for (_ >= v && (v = _ + 1); !(b = g[v]) && ++v < m;);
			y._next = b || null;
		}
	}
	return o = new Rt(o, r), o._enter = s, o._exit = c, o;
}
function xe(e) {
	return typeof e == "object" && "length" in e ? e : Array.from(e);
}
// node_modules/d3-selection/src/selection/exit.js
function Se() {
	return new Rt(this._exit || this._groups.map(pe), this._parents);
}
// node_modules/d3-selection/src/selection/join.js
function Ce(e, t, n) {
	var r = this.enter(), i = this, a = this.exit();
	return typeof e == "function" ? (r = e(r), r &&= r.selection()) : r = r.append(e + ""), t != null && (i = t(i), i &&= i.selection()), n == null ? a.remove() : n(a), r && i ? r.merge(i).order() : i;
}
// node_modules/d3-selection/src/selection/merge.js
function we(e) {
	for (var t = e.selection ? e.selection() : e, n = this._groups, r = t._groups, i = n.length, a = r.length, o = Math.min(i, a), s = Array(i), c = 0; c < o; ++c) for (var l = n[c], u = r[c], d = l.length, f = s[c] = Array(d), p, m = 0; m < d; ++m) (p = l[m] || u[m]) && (f[m] = p);
	for (; c < i; ++c) s[c] = n[c];
	return new Rt(s, this._parents);
}
// node_modules/d3-selection/src/selection/order.js
function Te() {
	for (var e = this._groups, t = -1, n = e.length; ++t < n;) for (var r = e[t], i = r.length - 1, a = r[i], o; --i >= 0;) (o = r[i]) && (a && o.compareDocumentPosition(a) ^ 4 && a.parentNode.insertBefore(o, a), a = o);
	return this;
}
// node_modules/d3-selection/src/selection/sort.js
function Ee(e) {
	e ||= De;
	function t(t, n) {
		return t && n ? e(t.__data__, n.__data__) : !t - !n;
	}
	for (var n = this._groups, r = n.length, i = Array(r), a = 0; a < r; ++a) {
		for (var o = n[a], s = o.length, c = i[a] = Array(s), l, u = 0; u < s; ++u) (l = o[u]) && (c[u] = l);
		c.sort(t);
	}
	return new Rt(i, this._parents).order();
}
function De(e, t) {
	return e < t ? -1 : e > t ? 1 : e >= t ? 0 : NaN;
}
// node_modules/d3-selection/src/selection/call.js
function Oe() {
	var e = arguments[0];
	return arguments[0] = this, e.apply(null, arguments), this;
}
// node_modules/d3-selection/src/selection/nodes.js
function ke() {
	return Array.from(this);
}
// node_modules/d3-selection/src/selection/node.js
function Ae() {
	for (var e = this._groups, t = 0, n = e.length; t < n; ++t) for (var r = e[t], i = 0, a = r.length; i < a; ++i) {
		var o = r[i];
		if (o) return o;
	}
	return null;
}
// node_modules/d3-selection/src/selection/size.js
function je() {
	let e = 0;
	for (let t of this) ++e;
	return e;
}
// node_modules/d3-selection/src/selection/empty.js
function Me() {
	return !this.node();
}
// node_modules/d3-selection/src/selection/each.js
function Ne(e) {
	for (var t = this._groups, n = 0, r = t.length; n < r; ++n) for (var i = t[n], a = 0, o = i.length, s; a < o; ++a) (s = i[a]) && e.call(s, s.__data__, a, i);
	return this;
}
// node_modules/d3-selection/src/selection/attr.js
function Pe(e) {
	return function() {
		this.removeAttribute(e);
	};
}
function Fe(e) {
	return function() {
		this.removeAttributeNS(e.space, e.local);
	};
}
function Ie(e, t) {
	return function() {
		this.setAttribute(e, t);
	};
}
function Le(e, t) {
	return function() {
		this.setAttributeNS(e.space, e.local, t);
	};
}
function Re(e, t) {
	return function() {
		var n = t.apply(this, arguments);
		n == null ? this.removeAttribute(e) : this.setAttribute(e, n);
	};
}
function ze(e, t) {
	return function() {
		var n = t.apply(this, arguments);
		n == null ? this.removeAttributeNS(e.space, e.local) : this.setAttributeNS(e.space, e.local, n);
	};
}
function Be(e, t) {
	var n = A(e);
	if (arguments.length < 2) {
		var r = this.node();
		return n.local ? r.getAttributeNS(n.space, n.local) : r.getAttribute(n);
	}
	return this.each((t == null ? n.local ? Fe : Pe : typeof t == "function" ? n.local ? ze : Re : n.local ? Le : Ie)(n, t));
}
// node_modules/d3-selection/src/window.js
function Ve(e) {
	return e.ownerDocument && e.ownerDocument.defaultView || e.document && e || e.defaultView;
}
// node_modules/d3-selection/src/selection/style.js
function He(e) {
	return function() {
		this.style.removeProperty(e);
	};
}
function Ue(e, t, n) {
	return function() {
		this.style.setProperty(e, t, n);
	};
}
function We(e, t, n) {
	return function() {
		var r = t.apply(this, arguments);
		r == null ? this.style.removeProperty(e) : this.style.setProperty(e, r, n);
	};
}
function Ge(e, t, n) {
	return arguments.length > 1 ? this.each((t == null ? He : typeof t == "function" ? We : Ue)(e, t, n ?? "")) : Ke(this.node(), e);
}
function Ke(e, t) {
	return e.style.getPropertyValue(t) || Ve(e).getComputedStyle(e, null).getPropertyValue(t);
}
// node_modules/d3-selection/src/selection/property.js
function qe(e) {
	return function() {
		delete this[e];
	};
}
function Je(e, t) {
	return function() {
		this[e] = t;
	};
}
function Ye(e, t) {
	return function() {
		var n = t.apply(this, arguments);
		n == null ? delete this[e] : this[e] = n;
	};
}
function Xe(e, t) {
	return arguments.length > 1 ? this.each((t == null ? qe : typeof t == "function" ? Ye : Je)(e, t)) : this.node()[e];
}
// node_modules/d3-selection/src/selection/classed.js
function Ze(e) {
	return e.trim().split(/^|\s+/);
}
function Qe(e) {
	return e.classList || new $e(e);
}
function $e(e) {
	this._node = e, this._names = Ze(e.getAttribute("class") || "");
}
$e.prototype = {
	add: function(e) {
		this._names.indexOf(e) < 0 && (this._names.push(e), this._node.setAttribute("class", this._names.join(" ")));
	},
	remove: function(e) {
		var t = this._names.indexOf(e);
		t >= 0 && (this._names.splice(t, 1), this._node.setAttribute("class", this._names.join(" ")));
	},
	contains: function(e) {
		return this._names.indexOf(e) >= 0;
	}
};
function et(e, t) {
	for (var n = Qe(e), r = -1, i = t.length; ++r < i;) n.add(t[r]);
}
function tt(e, t) {
	for (var n = Qe(e), r = -1, i = t.length; ++r < i;) n.remove(t[r]);
}
function nt(e) {
	return function() {
		et(this, e);
	};
}
function rt(e) {
	return function() {
		tt(this, e);
	};
}
function it(e, t) {
	return function() {
		(t.apply(this, arguments) ? et : tt)(this, e);
	};
}
function at(e, t) {
	var n = Ze(e + "");
	if (arguments.length < 2) {
		for (var r = Qe(this.node()), i = -1, a = n.length; ++i < a;) if (!r.contains(n[i])) return !1;
		return !0;
	}
	return this.each((typeof t == "function" ? it : t ? nt : rt)(n, t));
}
// node_modules/d3-selection/src/selection/text.js
function ot() {
	this.textContent = "";
}
function st(e) {
	return function() {
		this.textContent = e;
	};
}
function ct(e) {
	return function() {
		var t = e.apply(this, arguments);
		this.textContent = t ?? "";
	};
}
function lt(e) {
	return arguments.length ? this.each(e == null ? ot : (typeof e == "function" ? ct : st)(e)) : this.node().textContent;
}
// node_modules/d3-selection/src/selection/html.js
function ut() {
	this.innerHTML = "";
}
function dt(e) {
	return function() {
		this.innerHTML = e;
	};
}
function ft(e) {
	return function() {
		var t = e.apply(this, arguments);
		this.innerHTML = t ?? "";
	};
}
function pt(e) {
	return arguments.length ? this.each(e == null ? ut : (typeof e == "function" ? ft : dt)(e)) : this.node().innerHTML;
}
// node_modules/d3-selection/src/selection/raise.js
function mt() {
	this.nextSibling && this.parentNode.appendChild(this);
}
function ht() {
	return this.each(mt);
}
// node_modules/d3-selection/src/selection/lower.js
function gt() {
	this.previousSibling && this.parentNode.insertBefore(this, this.parentNode.firstChild);
}
function _t() {
	return this.each(gt);
}
// node_modules/d3-selection/src/selection/append.js
function vt(e) {
	var t = typeof e == "function" ? e : ee(e);
	return this.select(function() {
		return this.appendChild(t.apply(this, arguments));
	});
}
// node_modules/d3-selection/src/selection/insert.js
function yt() {
	return null;
}
function bt(e, t) {
	var n = typeof e == "function" ? e : ee(e), r = t == null ? yt : typeof t == "function" ? t : te(t);
	return this.select(function() {
		return this.insertBefore(n.apply(this, arguments), r.apply(this, arguments) || null);
	});
}
// node_modules/d3-selection/src/selection/remove.js
function xt() {
	var e = this.parentNode;
	e && e.removeChild(this);
}
function St() {
	return this.each(xt);
}
// node_modules/d3-selection/src/selection/clone.js
function Ct() {
	var e = this.cloneNode(!1), t = this.parentNode;
	return t ? t.insertBefore(e, this.nextSibling) : e;
}
function wt() {
	var e = this.cloneNode(!0), t = this.parentNode;
	return t ? t.insertBefore(e, this.nextSibling) : e;
}
function Tt(e) {
	return this.select(e ? wt : Ct);
}
// node_modules/d3-selection/src/selection/datum.js
function Et(e) {
	return arguments.length ? this.property("__data__", e) : this.node().__data__;
}
// node_modules/d3-selection/src/selection/on.js
function Dt(e) {
	return function(t) {
		e.call(this, t, this.__data__);
	};
}
function Ot(e) {
	return e.trim().split(/^|\s+/).map(function(e) {
		var t = "", n = e.indexOf(".");
		return n >= 0 && (t = e.slice(n + 1), e = e.slice(0, n)), {
			type: e,
			name: t
		};
	});
}
function kt(e) {
	return function() {
		var t = this.__on;
		if (t) {
			for (var n = 0, r = -1, i = t.length, a; n < i; ++n) a = t[n], (!e.type || a.type === e.type) && a.name === e.name ? this.removeEventListener(a.type, a.listener, a.options) : t[++r] = a;
			++r ? t.length = r : delete this.__on;
		}
	};
}
function At(e, t, n) {
	return function() {
		var r = this.__on, i, a = Dt(t);
		if (r) {
			for (var o = 0, s = r.length; o < s; ++o) if ((i = r[o]).type === e.type && i.name === e.name) {
				this.removeEventListener(i.type, i.listener, i.options), this.addEventListener(i.type, i.listener = a, i.options = n), i.value = t;
				return;
			}
		}
		this.addEventListener(e.type, a, n), i = {
			type: e.type,
			name: e.name,
			value: t,
			listener: a,
			options: n
		}, r ? r.push(i) : this.__on = [i];
	};
}
function jt(e, t, n) {
	var r = Ot(e + ""), i, a = r.length, o;
	if (arguments.length < 2) {
		var s = this.node().__on;
		if (s) {
			for (var c = 0, l = s.length, u; c < l; ++c) for (i = 0, u = s[c]; i < a; ++i) if ((o = r[i]).type === u.type && o.name === u.name) return u.value;
		}
	} else {
		for (s = t ? At : kt, i = 0; i < a; ++i) this.each(s(r[i], t, n));
		return this;
	}
}
// node_modules/d3-selection/src/selection/dispatch.js
function Mt(e, t, n) {
	var r = Ve(e), i = r.CustomEvent;
	typeof i == "function" ? i = new i(t, n) : (i = r.document.createEvent("Event"), n ? (i.initEvent(t, n.bubbles, n.cancelable), i.detail = n.detail) : i.initEvent(t, !1, !1)), e.dispatchEvent(i);
}
function Nt(e, t) {
	return function() {
		return Mt(this, e, t);
	};
}
function Pt(e, t) {
	return function() {
		return Mt(this, e, t.apply(this, arguments));
	};
}
function Ft(e, t) {
	return this.each((typeof t == "function" ? Pt : Nt)(e, t));
}
// node_modules/d3-selection/src/selection/iterator.js
function* It() {
	for (var e = this._groups, t = 0, n = e.length; t < n; ++t) for (var r = e[t], i = 0, a = r.length, o; i < a; ++i) (o = r[i]) && (yield o);
}
// node_modules/d3-selection/src/selection/index.js
var Lt = [null];
function Rt(e, t) {
	this._groups = e, this._parents = t;
}
function zt() {
	return new Rt([[document.documentElement]], Lt);
}
function Bt() {
	return this;
}
Rt.prototype = zt.prototype = {
	constructor: Rt,
	select: P,
	selectAll: L,
	selectChild: se,
	selectChildren: de,
	filter: fe,
	data: be,
	enter: me,
	exit: Se,
	join: Ce,
	merge: we,
	selection: Bt,
	order: Te,
	sort: Ee,
	call: Oe,
	nodes: ke,
	node: Ae,
	size: je,
	empty: Me,
	each: Ne,
	attr: Be,
	style: Ge,
	property: Xe,
	classed: at,
	text: lt,
	html: pt,
	raise: ht,
	lower: _t,
	append: vt,
	insert: bt,
	remove: St,
	clone: Tt,
	datum: Et,
	on: jt,
	dispatch: Ft,
	[Symbol.iterator]: It
};
// node_modules/d3-selection/src/select.js
function Vt(e) {
	return typeof e == "string" ? new Rt([[document.querySelector(e)]], [document.documentElement]) : new Rt([[e]], Lt);
}
// node_modules/d3-selection/src/sourceEvent.js
function Ht(e) {
	let t;
	for (; t = e.sourceEvent;) e = t;
	return e;
}
// node_modules/d3-selection/src/pointer.js
function Ut(e, t) {
	if (e = Ht(e), t === void 0 && (t = e.currentTarget), t) {
		var n = t.ownerSVGElement || t;
		if (n.createSVGPoint) {
			var r = n.createSVGPoint();
			return r.x = e.clientX, r.y = e.clientY, r = r.matrixTransform(t.getScreenCTM().inverse()), [r.x, r.y];
		}
		if (t.getBoundingClientRect) {
			var i = t.getBoundingClientRect();
			return [e.clientX - i.left - t.clientLeft, e.clientY - i.top - t.clientTop];
		}
	}
	return [e.pageX, e.pageY];
}
// node_modules/d3-drag/src/noevent.js
var Wt = { passive: !1 }, Gt = {
	capture: !0,
	passive: !1
};
function Kt(e) {
	e.stopImmediatePropagation();
}
function qt(e) {
	e.preventDefault(), e.stopImmediatePropagation();
}
// node_modules/d3-drag/src/nodrag.js
function Jt(e) {
	var t = e.document.documentElement, n = Vt(e).on("dragstart.drag", qt, Gt);
	"onselectstart" in t ? n.on("selectstart.drag", qt, Gt) : (t.__noselect = t.style.MozUserSelect, t.style.MozUserSelect = "none");
}
function Yt(e, t) {
	var n = e.document.documentElement, r = Vt(e).on("dragstart.drag", null);
	t && (r.on("click.drag", qt, Gt), setTimeout(function() {
		r.on("click.drag", null);
	}, 0)), "onselectstart" in n ? r.on("selectstart.drag", null) : (n.style.MozUserSelect = n.__noselect, delete n.__noselect);
}
// node_modules/d3-drag/src/constant.js
var Xt = (e) => () => e;
// node_modules/d3-drag/src/event.js
function Zt(e, { sourceEvent: t, subject: n, target: r, identifier: i, active: a, x: o, y: s, dx: c, dy: l, dispatch: u }) {
	Object.defineProperties(this, {
		type: {
			value: e,
			enumerable: !0,
			configurable: !0
		},
		sourceEvent: {
			value: t,
			enumerable: !0,
			configurable: !0
		},
		subject: {
			value: n,
			enumerable: !0,
			configurable: !0
		},
		target: {
			value: r,
			enumerable: !0,
			configurable: !0
		},
		identifier: {
			value: i,
			enumerable: !0,
			configurable: !0
		},
		active: {
			value: a,
			enumerable: !0,
			configurable: !0
		},
		x: {
			value: o,
			enumerable: !0,
			configurable: !0
		},
		y: {
			value: s,
			enumerable: !0,
			configurable: !0
		},
		dx: {
			value: c,
			enumerable: !0,
			configurable: !0
		},
		dy: {
			value: l,
			enumerable: !0,
			configurable: !0
		},
		_: { value: u }
	});
}
Zt.prototype.on = function() {
	var e = this._.on.apply(this._, arguments);
	return e === this._ ? this : e;
};
// node_modules/d3-drag/src/drag.js
function Qt(e) {
	return !e.ctrlKey && !e.button;
}
function $t() {
	return this.parentNode;
}
function en(e, t) {
	return t ?? {
		x: e.x,
		y: e.y
	};
}
function tn() {
	return navigator.maxTouchPoints || "ontouchstart" in this;
}
function nn() {
	var e = Qt, t = $t, n = en, r = tn, i = {}, a = w("start", "drag", "end"), o = 0, s, c, l, u, d = 0;
	function f(e) {
		e.on("mousedown.drag", p).filter(r).on("touchstart.drag", g).on("touchmove.drag", _, Wt).on("touchend.drag touchcancel.drag", v).style("touch-action", "none").style("-webkit-tap-highlight-color", "rgba(0,0,0,0)");
	}
	function p(n, r) {
		if (!u && e.call(this, n, r)) {
			var i = y(this, t.call(this, n, r), n, r, "mouse");
			i && (Vt(n.view).on("mousemove.drag", m, Gt).on("mouseup.drag", h, Gt), Jt(n.view), Kt(n), l = !1, s = n.clientX, c = n.clientY, i("start", n));
		}
	}
	function m(e) {
		if (qt(e), !l) {
			var t = e.clientX - s, n = e.clientY - c;
			l = t * t + n * n > d;
		}
		i.mouse("drag", e);
	}
	function h(e) {
		Vt(e.view).on("mousemove.drag mouseup.drag", null), Yt(e.view, l), qt(e), i.mouse("end", e);
	}
	function g(n, r) {
		if (e.call(this, n, r)) for (var i = n.changedTouches, a = t.call(this, n, r), o = i.length, s = 0, c; s < o; ++s) (c = y(this, a, n, r, i[s].identifier, i[s])) && (Kt(n), c("start", n, i[s]));
	}
	function _(e) {
		for (var t = e.changedTouches, n = t.length, r = 0, a; r < n; ++r) (a = i[t[r].identifier]) && (qt(e), a("drag", e, t[r]));
	}
	function v(e) {
		var t = e.changedTouches, n = t.length, r, a;
		for (u && clearTimeout(u), u = setTimeout(function() {
			u = null;
		}, 500), r = 0; r < n; ++r) (a = i[t[r].identifier]) && (Kt(e), a("end", e, t[r]));
	}
	function y(e, t, r, s, c, l) {
		var u = a.copy(), d = Ut(l || r, t), p, m, h;
		if ((h = n.call(e, new Zt("beforestart", {
			sourceEvent: r,
			target: f,
			identifier: c,
			active: o,
			x: d[0],
			y: d[1],
			dx: 0,
			dy: 0,
			dispatch: u
		}), s)) != null) return p = h.x - d[0] || 0, m = h.y - d[1] || 0, function n(r, a, l) {
			var g = d, _;
			switch (r) {
				case "start":
					i[c] = n, _ = o++;
					break;
				case "end": delete i[c], --o;
				case "drag": d = Ut(l || a, t), _ = o;
			}
			u.call(r, e, new Zt(r, {
				sourceEvent: a,
				subject: h,
				target: f,
				identifier: c,
				active: _,
				x: d[0] + p,
				y: d[1] + m,
				dx: d[0] - g[0],
				dy: d[1] - g[1],
				dispatch: u
			}), s);
		};
	}
	return f.filter = function(t) {
		return arguments.length ? (e = typeof t == "function" ? t : Xt(!!t), f) : e;
	}, f.container = function(e) {
		return arguments.length ? (t = typeof e == "function" ? e : Xt(e), f) : t;
	}, f.subject = function(e) {
		return arguments.length ? (n = typeof e == "function" ? e : Xt(e), f) : n;
	}, f.touchable = function(e) {
		return arguments.length ? (r = typeof e == "function" ? e : Xt(!!e), f) : r;
	}, f.on = function() {
		var e = a.on.apply(a, arguments);
		return e === a ? f : e;
	}, f.clickDistance = function(e) {
		return arguments.length ? (d = (e = +e) * e, f) : Math.sqrt(d);
	}, f;
}
// node_modules/d3-color/src/define.js
function rn(e, t, n) {
	e.prototype = t.prototype = n, n.constructor = e;
}
function an(e, t) {
	var n = Object.create(e.prototype);
	for (var r in t) n[r] = t[r];
	return n;
}
// node_modules/d3-color/src/color.js
function on() {}
var sn = .7, cn = 1 / sn, ln = "\\s*([+-]?\\d+)\\s*", un = "\\s*([+-]?(?:\\d*\\.)?\\d+(?:[eE][+-]?\\d+)?)\\s*", dn = "\\s*([+-]?(?:\\d*\\.)?\\d+(?:[eE][+-]?\\d+)?)%\\s*", fn = /^#([0-9a-f]{3,8})$/, pn = RegExp(`^rgb\\(${ln},${ln},${ln}\\)$`), mn = RegExp(`^rgb\\(${dn},${dn},${dn}\\)$`), hn = RegExp(`^rgba\\(${ln},${ln},${ln},${un}\\)$`), gn = RegExp(`^rgba\\(${dn},${dn},${dn},${un}\\)$`), _n = RegExp(`^hsl\\(${un},${dn},${dn}\\)$`), vn = RegExp(`^hsla\\(${un},${dn},${dn},${un}\\)$`), yn = {
	aliceblue: 15792383,
	antiquewhite: 16444375,
	aqua: 65535,
	aquamarine: 8388564,
	azure: 15794175,
	beige: 16119260,
	bisque: 16770244,
	black: 0,
	blanchedalmond: 16772045,
	blue: 255,
	blueviolet: 9055202,
	brown: 10824234,
	burlywood: 14596231,
	cadetblue: 6266528,
	chartreuse: 8388352,
	chocolate: 13789470,
	coral: 16744272,
	cornflowerblue: 6591981,
	cornsilk: 16775388,
	crimson: 14423100,
	cyan: 65535,
	darkblue: 139,
	darkcyan: 35723,
	darkgoldenrod: 12092939,
	darkgray: 11119017,
	darkgreen: 25600,
	darkgrey: 11119017,
	darkkhaki: 12433259,
	darkmagenta: 9109643,
	darkolivegreen: 5597999,
	darkorange: 16747520,
	darkorchid: 10040012,
	darkred: 9109504,
	darksalmon: 15308410,
	darkseagreen: 9419919,
	darkslateblue: 4734347,
	darkslategray: 3100495,
	darkslategrey: 3100495,
	darkturquoise: 52945,
	darkviolet: 9699539,
	deeppink: 16716947,
	deepskyblue: 49151,
	dimgray: 6908265,
	dimgrey: 6908265,
	dodgerblue: 2003199,
	firebrick: 11674146,
	floralwhite: 16775920,
	forestgreen: 2263842,
	fuchsia: 16711935,
	gainsboro: 14474460,
	ghostwhite: 16316671,
	gold: 16766720,
	goldenrod: 14329120,
	gray: 8421504,
	green: 32768,
	greenyellow: 11403055,
	grey: 8421504,
	honeydew: 15794160,
	hotpink: 16738740,
	indianred: 13458524,
	indigo: 4915330,
	ivory: 16777200,
	khaki: 15787660,
	lavender: 15132410,
	lavenderblush: 16773365,
	lawngreen: 8190976,
	lemonchiffon: 16775885,
	lightblue: 11393254,
	lightcoral: 15761536,
	lightcyan: 14745599,
	lightgoldenrodyellow: 16448210,
	lightgray: 13882323,
	lightgreen: 9498256,
	lightgrey: 13882323,
	lightpink: 16758465,
	lightsalmon: 16752762,
	lightseagreen: 2142890,
	lightskyblue: 8900346,
	lightslategray: 7833753,
	lightslategrey: 7833753,
	lightsteelblue: 11584734,
	lightyellow: 16777184,
	lime: 65280,
	limegreen: 3329330,
	linen: 16445670,
	magenta: 16711935,
	maroon: 8388608,
	mediumaquamarine: 6737322,
	mediumblue: 205,
	mediumorchid: 12211667,
	mediumpurple: 9662683,
	mediumseagreen: 3978097,
	mediumslateblue: 8087790,
	mediumspringgreen: 64154,
	mediumturquoise: 4772300,
	mediumvioletred: 13047173,
	midnightblue: 1644912,
	mintcream: 16121850,
	mistyrose: 16770273,
	moccasin: 16770229,
	navajowhite: 16768685,
	navy: 128,
	oldlace: 16643558,
	olive: 8421376,
	olivedrab: 7048739,
	orange: 16753920,
	orangered: 16729344,
	orchid: 14315734,
	palegoldenrod: 15657130,
	palegreen: 10025880,
	paleturquoise: 11529966,
	palevioletred: 14381203,
	papayawhip: 16773077,
	peachpuff: 16767673,
	peru: 13468991,
	pink: 16761035,
	plum: 14524637,
	powderblue: 11591910,
	purple: 8388736,
	rebeccapurple: 6697881,
	red: 16711680,
	rosybrown: 12357519,
	royalblue: 4286945,
	saddlebrown: 9127187,
	salmon: 16416882,
	sandybrown: 16032864,
	seagreen: 3050327,
	seashell: 16774638,
	sienna: 10506797,
	silver: 12632256,
	skyblue: 8900331,
	slateblue: 6970061,
	slategray: 7372944,
	slategrey: 7372944,
	snow: 16775930,
	springgreen: 65407,
	steelblue: 4620980,
	tan: 13808780,
	teal: 32896,
	thistle: 14204888,
	tomato: 16737095,
	turquoise: 4251856,
	violet: 15631086,
	wheat: 16113331,
	white: 16777215,
	whitesmoke: 16119285,
	yellow: 16776960,
	yellowgreen: 10145074
};
rn(on, wn, {
	copy(e) {
		return Object.assign(new this.constructor(), this, e);
	},
	displayable() {
		return this.rgb().displayable();
	},
	hex: bn,
	formatHex: bn,
	formatHex8: xn,
	formatHsl: Sn,
	formatRgb: Cn,
	toString: Cn
});
function bn() {
	return this.rgb().formatHex();
}
function xn() {
	return this.rgb().formatHex8();
}
function Sn() {
	return Ln(this).formatHsl();
}
function Cn() {
	return this.rgb().formatRgb();
}
function wn(e) {
	var t, n;
	return e = (e + "").trim().toLowerCase(), (t = fn.exec(e)) ? (n = t[1].length, t = parseInt(t[1], 16), n === 6 ? Tn(t) : n === 3 ? new kn(t >> 8 & 15 | t >> 4 & 240, t >> 4 & 15 | t & 240, (t & 15) << 4 | t & 15, 1) : n === 8 ? En(t >> 24 & 255, t >> 16 & 255, t >> 8 & 255, (t & 255) / 255) : n === 4 ? En(t >> 12 & 15 | t >> 8 & 240, t >> 8 & 15 | t >> 4 & 240, t >> 4 & 15 | t & 240, ((t & 15) << 4 | t & 15) / 255) : null) : (t = pn.exec(e)) ? new kn(t[1], t[2], t[3], 1) : (t = mn.exec(e)) ? new kn(t[1] * 255 / 100, t[2] * 255 / 100, t[3] * 255 / 100, 1) : (t = hn.exec(e)) ? En(t[1], t[2], t[3], t[4]) : (t = gn.exec(e)) ? En(t[1] * 255 / 100, t[2] * 255 / 100, t[3] * 255 / 100, t[4]) : (t = _n.exec(e)) ? In(t[1], t[2] / 100, t[3] / 100, 1) : (t = vn.exec(e)) ? In(t[1], t[2] / 100, t[3] / 100, t[4]) : yn.hasOwnProperty(e) ? Tn(yn[e]) : e === "transparent" ? new kn(NaN, NaN, NaN, 0) : null;
}
function Tn(e) {
	return new kn(e >> 16 & 255, e >> 8 & 255, e & 255, 1);
}
function En(e, t, n, r) {
	return r <= 0 && (e = t = n = NaN), new kn(e, t, n, r);
}
function Dn(e) {
	return e instanceof on || (e = wn(e)), e ? (e = e.rgb(), new kn(e.r, e.g, e.b, e.opacity)) : new kn();
}
function On(e, t, n, r) {
	return arguments.length === 1 ? Dn(e) : new kn(e, t, n, r ?? 1);
}
function kn(e, t, n, r) {
	this.r = +e, this.g = +t, this.b = +n, this.opacity = +r;
}
rn(kn, On, an(on, {
	brighter(e) {
		return e = e == null ? cn : cn ** +e, new kn(this.r * e, this.g * e, this.b * e, this.opacity);
	},
	darker(e) {
		return e = e == null ? sn : sn ** +e, new kn(this.r * e, this.g * e, this.b * e, this.opacity);
	},
	rgb() {
		return this;
	},
	clamp() {
		return new kn(Pn(this.r), Pn(this.g), Pn(this.b), Nn(this.opacity));
	},
	displayable() {
		return -.5 <= this.r && this.r < 255.5 && -.5 <= this.g && this.g < 255.5 && -.5 <= this.b && this.b < 255.5 && 0 <= this.opacity && this.opacity <= 1;
	},
	hex: An,
	formatHex: An,
	formatHex8: jn,
	formatRgb: Mn,
	toString: Mn
}));
function An() {
	return `#${Fn(this.r)}${Fn(this.g)}${Fn(this.b)}`;
}
function jn() {
	return `#${Fn(this.r)}${Fn(this.g)}${Fn(this.b)}${Fn((isNaN(this.opacity) ? 1 : this.opacity) * 255)}`;
}
function Mn() {
	let e = Nn(this.opacity);
	return `${e === 1 ? "rgb(" : "rgba("}${Pn(this.r)}, ${Pn(this.g)}, ${Pn(this.b)}${e === 1 ? ")" : `, ${e})`}`;
}
function Nn(e) {
	return isNaN(e) ? 1 : Math.max(0, Math.min(1, e));
}
function Pn(e) {
	return Math.max(0, Math.min(255, Math.round(e) || 0));
}
function Fn(e) {
	return e = Pn(e), (e < 16 ? "0" : "") + e.toString(16);
}
function In(e, t, n, r) {
	return r <= 0 ? e = t = n = NaN : n <= 0 || n >= 1 ? e = t = NaN : t <= 0 && (e = NaN), new zn(e, t, n, r);
}
function Ln(e) {
	if (e instanceof zn) return new zn(e.h, e.s, e.l, e.opacity);
	if (e instanceof on || (e = wn(e)), !e) return new zn();
	if (e instanceof zn) return e;
	e = e.rgb();
	var t = e.r / 255, n = e.g / 255, r = e.b / 255, i = Math.min(t, n, r), a = Math.max(t, n, r), o = NaN, s = a - i, c = (a + i) / 2;
	return s ? (o = t === a ? (n - r) / s + (n < r) * 6 : n === a ? (r - t) / s + 2 : (t - n) / s + 4, s /= c < .5 ? a + i : 2 - a - i, o *= 60) : s = c > 0 && c < 1 ? 0 : o, new zn(o, s, c, e.opacity);
}
function Rn(e, t, n, r) {
	return arguments.length === 1 ? Ln(e) : new zn(e, t, n, r ?? 1);
}
function zn(e, t, n, r) {
	this.h = +e, this.s = +t, this.l = +n, this.opacity = +r;
}
rn(zn, Rn, an(on, {
	brighter(e) {
		return e = e == null ? cn : cn ** +e, new zn(this.h, this.s, this.l * e, this.opacity);
	},
	darker(e) {
		return e = e == null ? sn : sn ** +e, new zn(this.h, this.s, this.l * e, this.opacity);
	},
	rgb() {
		var e = this.h % 360 + (this.h < 0) * 360, t = isNaN(e) || isNaN(this.s) ? 0 : this.s, n = this.l, r = n + (n < .5 ? n : 1 - n) * t, i = 2 * n - r;
		return new kn(Hn(e >= 240 ? e - 240 : e + 120, i, r), Hn(e, i, r), Hn(e < 120 ? e + 240 : e - 120, i, r), this.opacity);
	},
	clamp() {
		return new zn(Bn(this.h), Vn(this.s), Vn(this.l), Nn(this.opacity));
	},
	displayable() {
		return (0 <= this.s && this.s <= 1 || isNaN(this.s)) && 0 <= this.l && this.l <= 1 && 0 <= this.opacity && this.opacity <= 1;
	},
	formatHsl() {
		let e = Nn(this.opacity);
		return `${e === 1 ? "hsl(" : "hsla("}${Bn(this.h)}, ${Vn(this.s) * 100}%, ${Vn(this.l) * 100}%${e === 1 ? ")" : `, ${e})`}`;
	}
}));
function Bn(e) {
	return e = (e || 0) % 360, e < 0 ? e + 360 : e;
}
function Vn(e) {
	return Math.max(0, Math.min(1, e || 0));
}
function Hn(e, t, n) {
	return (e < 60 ? t + (n - t) * e / 60 : e < 180 ? n : e < 240 ? t + (n - t) * (240 - e) / 60 : t) * 255;
}
// node_modules/d3-interpolate/src/constant.js
var Un = (e) => () => e;
// node_modules/d3-interpolate/src/color.js
function Wn(e, t) {
	return function(n) {
		return e + n * t;
	};
}
function Gn(e, t, n) {
	return e **= +n, t = t ** +n - e, n = 1 / n, function(r) {
		return (e + r * t) ** +n;
	};
}
function Kn(e) {
	return (e = +e) == 1 ? qn : function(t, n) {
		return n - t ? Gn(t, n, e) : Un(isNaN(t) ? n : t);
	};
}
function qn(e, t) {
	var n = t - e;
	return n ? Wn(e, n) : Un(isNaN(e) ? t : e);
}
// node_modules/d3-interpolate/src/rgb.js
var Jn = (function e(t) {
	var n = Kn(t);
	function r(e, t) {
		var r = n((e = On(e)).r, (t = On(t)).r), i = n(e.g, t.g), a = n(e.b, t.b), o = qn(e.opacity, t.opacity);
		return function(t) {
			return e.r = r(t), e.g = i(t), e.b = a(t), e.opacity = o(t), e + "";
		};
	}
	return r.gamma = e, r;
})(1);
// node_modules/d3-interpolate/src/numberArray.js
function Yn(e, t) {
	t ||= [];
	var n = e ? Math.min(t.length, e.length) : 0, r = t.slice(), i;
	return function(a) {
		for (i = 0; i < n; ++i) r[i] = e[i] * (1 - a) + t[i] * a;
		return r;
	};
}
function Xn(e) {
	return ArrayBuffer.isView(e) && !(e instanceof DataView);
}
// node_modules/d3-interpolate/src/array.js
function Zn(e, t) {
	for (var n = t ? t.length : 0, r = e ? Math.min(n, e.length) : 0, i = Array(r), a = Array(n), o = 0; o < r; ++o) i[o] = or(e[o], t[o]);
	for (; o < n; ++o) a[o] = t[o];
	return function(e) {
		for (o = 0; o < r; ++o) a[o] = i[o](e);
		return a;
	};
}
// node_modules/d3-interpolate/src/date.js
function Qn(e, t) {
	var n = /* @__PURE__ */ new Date();
	return e = +e, t = +t, function(r) {
		return n.setTime(e * (1 - r) + t * r), n;
	};
}
// node_modules/d3-interpolate/src/number.js
function $n(e, t) {
	return e = +e, t = +t, function(n) {
		return e * (1 - n) + t * n;
	};
}
// node_modules/d3-interpolate/src/object.js
function er(e, t) {
	var n = {}, r = {}, i;
	for (i in typeof e == "object" && e || (e = {}), typeof t == "object" && t || (t = {}), t) i in e ? n[i] = or(e[i], t[i]) : r[i] = t[i];
	return function(e) {
		for (i in n) r[i] = n[i](e);
		return r;
	};
}
// node_modules/d3-interpolate/src/string.js
var tr = /[-+]?(?:\d+\.?\d*|\.?\d+)(?:[eE][-+]?\d+)?/g, nr = new RegExp(tr.source, "g");
function rr(e) {
	return function() {
		return e;
	};
}
function ir(e) {
	return function(t) {
		return e(t) + "";
	};
}
function ar(e, t) {
	var n = tr.lastIndex = nr.lastIndex = 0, r, i, a, o = -1, s = [], c = [];
	for (e += "", t += ""; (r = tr.exec(e)) && (i = nr.exec(t));) (a = i.index) > n && (a = t.slice(n, a), s[o] ? s[o] += a : s[++o] = a), (r = r[0]) === (i = i[0]) ? s[o] ? s[o] += i : s[++o] = i : (s[++o] = null, c.push({
		i: o,
		x: $n(r, i)
	})), n = nr.lastIndex;
	return n < t.length && (a = t.slice(n), s[o] ? s[o] += a : s[++o] = a), s.length < 2 ? c[0] ? ir(c[0].x) : rr(t) : (t = c.length, function(e) {
		for (var n = 0, r; n < t; ++n) s[(r = c[n]).i] = r.x(e);
		return s.join("");
	});
}
// node_modules/d3-interpolate/src/value.js
function or(e, t) {
	var n = typeof t, r;
	return t == null || n === "boolean" ? Un(t) : (n === "number" ? $n : n === "string" ? (r = wn(t)) ? (t = r, Jn) : ar : t instanceof wn ? Jn : t instanceof Date ? Qn : Xn(t) ? Yn : Array.isArray(t) ? Zn : typeof t.valueOf != "function" && typeof t.toString != "function" || isNaN(t) ? er : $n)(e, t);
}
// node_modules/d3-interpolate/src/transform/decompose.js
var sr = 180 / Math.PI, cr = {
	translateX: 0,
	translateY: 0,
	rotate: 0,
	skewX: 0,
	scaleX: 1,
	scaleY: 1
};
function lr(e, t, n, r, i, a) {
	var o, s, c;
	return (o = Math.sqrt(e * e + t * t)) && (e /= o, t /= o), (c = e * n + t * r) && (n -= e * c, r -= t * c), (s = Math.sqrt(n * n + r * r)) && (n /= s, r /= s, c /= s), e * r < t * n && (e = -e, t = -t, c = -c, o = -o), {
		translateX: i,
		translateY: a,
		rotate: Math.atan2(t, e) * sr,
		skewX: Math.atan(c) * sr,
		scaleX: o,
		scaleY: s
	};
}
// node_modules/d3-interpolate/src/transform/parse.js
var ur;
function dr(e) {
	let t = new (typeof DOMMatrix == "function" ? DOMMatrix : WebKitCSSMatrix)(e + "");
	return t.isIdentity ? cr : lr(t.a, t.b, t.c, t.d, t.e, t.f);
}
function fr(e) {
	return e == null || (ur ||= document.createElementNS("http://www.w3.org/2000/svg", "g"), ur.setAttribute("transform", e), !(e = ur.transform.baseVal.consolidate())) ? cr : (e = e.matrix, lr(e.a, e.b, e.c, e.d, e.e, e.f));
}
// node_modules/d3-interpolate/src/transform/index.js
function pr(e, t, n, r) {
	function i(e) {
		return e.length ? e.pop() + " " : "";
	}
	function a(e, r, i, a, o, s) {
		if (e !== i || r !== a) {
			var c = o.push("translate(", null, t, null, n);
			s.push({
				i: c - 4,
				x: $n(e, i)
			}, {
				i: c - 2,
				x: $n(r, a)
			});
		} else (i || a) && o.push("translate(" + i + t + a + n);
	}
	function o(e, t, n, a) {
		e === t ? t && n.push(i(n) + "rotate(" + t + r) : (e - t > 180 ? t += 360 : t - e > 180 && (e += 360), a.push({
			i: n.push(i(n) + "rotate(", null, r) - 2,
			x: $n(e, t)
		}));
	}
	function s(e, t, n, a) {
		e === t ? t && n.push(i(n) + "skewX(" + t + r) : a.push({
			i: n.push(i(n) + "skewX(", null, r) - 2,
			x: $n(e, t)
		});
	}
	function c(e, t, n, r, a, o) {
		if (e !== n || t !== r) {
			var s = a.push(i(a) + "scale(", null, ",", null, ")");
			o.push({
				i: s - 4,
				x: $n(e, n)
			}, {
				i: s - 2,
				x: $n(t, r)
			});
		} else (n !== 1 || r !== 1) && a.push(i(a) + "scale(" + n + "," + r + ")");
	}
	return function(t, n) {
		var r = [], i = [];
		return t = e(t), n = e(n), a(t.translateX, t.translateY, n.translateX, n.translateY, r, i), o(t.rotate, n.rotate, r, i), s(t.skewX, n.skewX, r, i), c(t.scaleX, t.scaleY, n.scaleX, n.scaleY, r, i), t = n = null, function(e) {
			for (var t = -1, n = i.length, a; ++t < n;) r[(a = i[t]).i] = a.x(e);
			return r.join("");
		};
	};
}
var mr = pr(dr, "px, ", "px)", "deg)"), hr = pr(fr, ", ", ")", ")"), gr = 1e-12;
function _r(e) {
	return ((e = Math.exp(e)) + 1 / e) / 2;
}
function vr(e) {
	return ((e = Math.exp(e)) - 1 / e) / 2;
}
function yr(e) {
	return ((e = Math.exp(2 * e)) - 1) / (e + 1);
}
var br = (function e(t, n, r) {
	function i(e, i) {
		var a = e[0], o = e[1], s = e[2], c = i[0], l = i[1], u = i[2], d = c - a, f = l - o, p = d * d + f * f, m, h;
		if (p < gr) h = Math.log(u / s) / t, m = function(e) {
			return [
				a + e * d,
				o + e * f,
				s * Math.exp(t * e * h)
			];
		};
		else {
			var g = Math.sqrt(p), _ = (u * u - s * s + r * p) / (2 * s * n * g), v = (u * u - s * s - r * p) / (2 * u * n * g), y = Math.log(Math.sqrt(_ * _ + 1) - _);
			h = (Math.log(Math.sqrt(v * v + 1) - v) - y) / t, m = function(e) {
				var r = e * h, i = _r(y), c = s / (n * g) * (i * yr(t * r + y) - vr(y));
				return [
					a + c * d,
					o + c * f,
					s * i / _r(t * r + y)
				];
			};
		}
		return m.duration = h * 1e3 * t / Math.SQRT2, m;
	}
	return i.rho = function(t) {
		var n = Math.max(.001, +t), r = n * n;
		return e(n, r, r * r);
	}, i;
})(Math.SQRT2, 2, 4), xr = 0, Sr = 0, Cr = 0, wr = 1e3, Tr, Er, Dr = 0, Or = 0, kr = 0, Ar = typeof performance == "object" && performance.now ? performance : Date, jr = typeof window == "object" && window.requestAnimationFrame ? window.requestAnimationFrame.bind(window) : function(e) {
	setTimeout(e, 17);
};
function Mr() {
	return Or ||= (jr(Nr), Ar.now() + kr);
}
function Nr() {
	Or = 0;
}
function Pr() {
	this._call = this._time = this._next = null;
}
Pr.prototype = Fr.prototype = {
	constructor: Pr,
	restart: function(e, t, n) {
		if (typeof e != "function") throw TypeError("callback is not a function");
		n = (n == null ? Mr() : +n) + (t == null ? 0 : +t), !this._next && Er !== this && (Er ? Er._next = this : Tr = this, Er = this), this._call = e, this._time = n, Br();
	},
	stop: function() {
		this._call && (this._call = null, this._time = Infinity, Br());
	}
};
function Fr(e, t, n) {
	var r = new Pr();
	return r.restart(e, t, n), r;
}
function Ir() {
	Mr(), ++xr;
	for (var e = Tr, t; e;) (t = Or - e._time) >= 0 && e._call.call(void 0, t), e = e._next;
	--xr;
}
function Lr() {
	Or = (Dr = Ar.now()) + kr, xr = Sr = 0;
	try {
		Ir();
	} finally {
		xr = 0, zr(), Or = 0;
	}
}
function Rr() {
	var e = Ar.now(), t = e - Dr;
	t > wr && (kr -= t, Dr = e);
}
function zr() {
	for (var e, t = Tr, n, r = Infinity; t;) t._call ? (r > t._time && (r = t._time), e = t, t = t._next) : (n = t._next, t._next = null, t = e ? e._next = n : Tr = n);
	Er = e, Br(r);
}
function Br(e) {
	xr || (Sr &&= clearTimeout(Sr), e - Or > 24 ? (e < Infinity && (Sr = setTimeout(Lr, e - Ar.now() - kr)), Cr &&= clearInterval(Cr)) : (Cr ||= (Dr = Ar.now(), setInterval(Rr, wr)), xr = 1, jr(Lr)));
}
// node_modules/d3-timer/src/timeout.js
function Vr(e, t, n) {
	var r = new Pr();
	return t = t == null ? 0 : +t, r.restart((n) => {
		r.stop(), e(n + t);
	}, t, n), r;
}
// node_modules/d3-transition/src/transition/schedule.js
var Hr = w("start", "end", "cancel", "interrupt"), Ur = [];
function Wr(e, t, n, r, i, a) {
	var o = e.__transition;
	if (!o) e.__transition = {};
	else if (n in o) return;
	Jr(e, n, {
		name: t,
		index: r,
		group: i,
		on: Hr,
		tween: Ur,
		time: a.time,
		delay: a.delay,
		duration: a.duration,
		ease: a.ease,
		timer: null,
		state: 0
	});
}
function Gr(e, t) {
	var n = qr(e, t);
	if (n.state > 0) throw Error("too late; already scheduled");
	return n;
}
function Kr(e, t) {
	var n = qr(e, t);
	if (n.state > 3) throw Error("too late; already running");
	return n;
}
function qr(e, t) {
	var n = e.__transition;
	if (!n || !(n = n[t])) throw Error("transition not found");
	return n;
}
function Jr(e, t, n) {
	var r = e.__transition, i;
	r[t] = n, n.timer = Fr(a, 0, n.time);
	function a(e) {
		n.state = 1, n.timer.restart(o, n.delay, n.time), n.delay <= e && o(e - n.delay);
	}
	function o(a) {
		var l, u, d, f;
		if (n.state !== 1) return c();
		for (l in r) if (f = r[l], f.name === n.name) {
			if (f.state === 3) return Vr(o);
			f.state === 4 ? (f.state = 6, f.timer.stop(), f.on.call("interrupt", e, e.__data__, f.index, f.group), delete r[l]) : +l < t && (f.state = 6, f.timer.stop(), f.on.call("cancel", e, e.__data__, f.index, f.group), delete r[l]);
		}
		if (Vr(function() {
			n.state === 3 && (n.state = 4, n.timer.restart(s, n.delay, n.time), s(a));
		}), n.state = 2, n.on.call("start", e, e.__data__, n.index, n.group), n.state === 2) {
			for (n.state = 3, i = Array(d = n.tween.length), l = 0, u = -1; l < d; ++l) (f = n.tween[l].value.call(e, e.__data__, n.index, n.group)) && (i[++u] = f);
			i.length = u + 1;
		}
	}
	function s(t) {
		for (var r = t < n.duration ? n.ease.call(null, t / n.duration) : (n.timer.restart(c), n.state = 5, 1), a = -1, o = i.length; ++a < o;) i[a].call(e, r);
		n.state === 5 && (n.on.call("end", e, e.__data__, n.index, n.group), c());
	}
	function c() {
		for (var i in n.state = 6, n.timer.stop(), delete r[t], r) return;
		delete e.__transition;
	}
}
// node_modules/d3-transition/src/interrupt.js
function Yr(e, t) {
	var n = e.__transition, r, i, a = !0, o;
	if (n) {
		for (o in t = t == null ? null : t + "", n) (r = n[o]).name === t ? (i = r.state > 2 && r.state < 5, r.state = 6, r.timer.stop(), r.on.call(i ? "interrupt" : "cancel", e, e.__data__, r.index, r.group), delete n[o]) : a = !1;
		a && delete e.__transition;
	}
}
// node_modules/d3-transition/src/selection/interrupt.js
function Xr(e) {
	return this.each(function() {
		Yr(this, e);
	});
}
// node_modules/d3-transition/src/transition/tween.js
function Zr(e, t) {
	var n, r;
	return function() {
		var i = Kr(this, e), a = i.tween;
		if (a !== n) {
			r = n = a;
			for (var o = 0, s = r.length; o < s; ++o) if (r[o].name === t) {
				r = r.slice(), r.splice(o, 1);
				break;
			}
		}
		i.tween = r;
	};
}
function Qr(e, t, n) {
	var r, i;
	if (typeof n != "function") throw Error();
	return function() {
		var a = Kr(this, e), o = a.tween;
		if (o !== r) {
			i = (r = o).slice();
			for (var s = {
				name: t,
				value: n
			}, c = 0, l = i.length; c < l; ++c) if (i[c].name === t) {
				i[c] = s;
				break;
			}
			c === l && i.push(s);
		}
		a.tween = i;
	};
}
function $r(e, t) {
	var n = this._id;
	if (e += "", arguments.length < 2) {
		for (var r = qr(this.node(), n).tween, i = 0, a = r.length, o; i < a; ++i) if ((o = r[i]).name === e) return o.value;
		return null;
	}
	return this.each((t == null ? Zr : Qr)(n, e, t));
}
function ei(e, t, n) {
	var r = e._id;
	return e.each(function() {
		var e = Kr(this, r);
		(e.value ||= {})[t] = n.apply(this, arguments);
	}), function(e) {
		return qr(e, r).value[t];
	};
}
// node_modules/d3-transition/src/transition/interpolate.js
function ti(e, t) {
	var n;
	return (typeof t == "number" ? $n : t instanceof wn ? Jn : (n = wn(t)) ? (t = n, Jn) : ar)(e, t);
}
// node_modules/d3-transition/src/transition/attr.js
function ni(e) {
	return function() {
		this.removeAttribute(e);
	};
}
function ri(e) {
	return function() {
		this.removeAttributeNS(e.space, e.local);
	};
}
function ii(e, t, n) {
	var r, i = n + "", a;
	return function() {
		var o = this.getAttribute(e);
		return o === i ? null : o === r ? a : a = t(r = o, n);
	};
}
function ai(e, t, n) {
	var r, i = n + "", a;
	return function() {
		var o = this.getAttributeNS(e.space, e.local);
		return o === i ? null : o === r ? a : a = t(r = o, n);
	};
}
function oi(e, t, n) {
	var r, i, a;
	return function() {
		var o, s = n(this), c;
		return s == null ? void this.removeAttribute(e) : (o = this.getAttribute(e), c = s + "", o === c ? null : o === r && c === i ? a : (i = c, a = t(r = o, s)));
	};
}
function si(e, t, n) {
	var r, i, a;
	return function() {
		var o, s = n(this), c;
		return s == null ? void this.removeAttributeNS(e.space, e.local) : (o = this.getAttributeNS(e.space, e.local), c = s + "", o === c ? null : o === r && c === i ? a : (i = c, a = t(r = o, s)));
	};
}
function ci(e, t) {
	var n = A(e), r = n === "transform" ? hr : ti;
	return this.attrTween(e, typeof t == "function" ? (n.local ? si : oi)(n, r, ei(this, "attr." + e, t)) : t == null ? (n.local ? ri : ni)(n) : (n.local ? ai : ii)(n, r, t));
}
// node_modules/d3-transition/src/transition/attrTween.js
function li(e, t) {
	return function(n) {
		this.setAttribute(e, t.call(this, n));
	};
}
function ui(e, t) {
	return function(n) {
		this.setAttributeNS(e.space, e.local, t.call(this, n));
	};
}
function di(e, t) {
	var n, r;
	function i() {
		var i = t.apply(this, arguments);
		return i !== r && (n = (r = i) && ui(e, i)), n;
	}
	return i._value = t, i;
}
function fi(e, t) {
	var n, r;
	function i() {
		var i = t.apply(this, arguments);
		return i !== r && (n = (r = i) && li(e, i)), n;
	}
	return i._value = t, i;
}
function pi(e, t) {
	var n = "attr." + e;
	if (arguments.length < 2) return (n = this.tween(n)) && n._value;
	if (t == null) return this.tween(n, null);
	if (typeof t != "function") throw Error();
	var r = A(e);
	return this.tween(n, (r.local ? di : fi)(r, t));
}
// node_modules/d3-transition/src/transition/delay.js
function mi(e, t) {
	return function() {
		Gr(this, e).delay = +t.apply(this, arguments);
	};
}
function hi(e, t) {
	return t = +t, function() {
		Gr(this, e).delay = t;
	};
}
function gi(e) {
	var t = this._id;
	return arguments.length ? this.each((typeof e == "function" ? mi : hi)(t, e)) : qr(this.node(), t).delay;
}
// node_modules/d3-transition/src/transition/duration.js
function _i(e, t) {
	return function() {
		Kr(this, e).duration = +t.apply(this, arguments);
	};
}
function vi(e, t) {
	return t = +t, function() {
		Kr(this, e).duration = t;
	};
}
function yi(e) {
	var t = this._id;
	return arguments.length ? this.each((typeof e == "function" ? _i : vi)(t, e)) : qr(this.node(), t).duration;
}
// node_modules/d3-transition/src/transition/ease.js
function bi(e, t) {
	if (typeof t != "function") throw Error();
	return function() {
		Kr(this, e).ease = t;
	};
}
function xi(e) {
	var t = this._id;
	return arguments.length ? this.each(bi(t, e)) : qr(this.node(), t).ease;
}
// node_modules/d3-transition/src/transition/easeVarying.js
function Si(e, t) {
	return function() {
		var n = t.apply(this, arguments);
		if (typeof n != "function") throw Error();
		Kr(this, e).ease = n;
	};
}
function Ci(e) {
	if (typeof e != "function") throw Error();
	return this.each(Si(this._id, e));
}
// node_modules/d3-transition/src/transition/filter.js
function wi(e) {
	typeof e != "function" && (e = R(e));
	for (var t = this._groups, n = t.length, r = Array(n), i = 0; i < n; ++i) for (var a = t[i], o = a.length, s = r[i] = [], c, l = 0; l < o; ++l) (c = a[l]) && e.call(c, c.__data__, l, a) && s.push(c);
	return new Qi(r, this._parents, this._name, this._id);
}
// node_modules/d3-transition/src/transition/merge.js
function Ti(e) {
	if (e._id !== this._id) throw Error();
	for (var t = this._groups, n = e._groups, r = t.length, i = n.length, a = Math.min(r, i), o = Array(r), s = 0; s < a; ++s) for (var c = t[s], l = n[s], u = c.length, d = o[s] = Array(u), f, p = 0; p < u; ++p) (f = c[p] || l[p]) && (d[p] = f);
	for (; s < r; ++s) o[s] = t[s];
	return new Qi(o, this._parents, this._name, this._id);
}
// node_modules/d3-transition/src/transition/on.js
function Ei(e) {
	return (e + "").trim().split(/^|\s+/).every(function(e) {
		var t = e.indexOf(".");
		return t >= 0 && (e = e.slice(0, t)), !e || e === "start";
	});
}
function Di(e, t, n) {
	var r, i, a = Ei(t) ? Gr : Kr;
	return function() {
		var o = a(this, e), s = o.on;
		s !== r && (i = (r = s).copy()).on(t, n), o.on = i;
	};
}
function Oi(e, t) {
	var n = this._id;
	return arguments.length < 2 ? qr(this.node(), n).on.on(e) : this.each(Di(n, e, t));
}
// node_modules/d3-transition/src/transition/remove.js
function B(e) {
	return function() {
		var t = this.parentNode;
		for (var n in this.__transition) if (+n !== e) return;
		t && t.removeChild(this);
	};
}
function ki() {
	return this.on("end.remove", B(this._id));
}
// node_modules/d3-transition/src/transition/select.js
function Ai(e) {
	var t = this._name, n = this._id;
	typeof e != "function" && (e = te(e));
	for (var r = this._groups, i = r.length, a = Array(i), o = 0; o < i; ++o) for (var s = r[o], c = s.length, l = a[o] = Array(c), u, d, f = 0; f < c; ++f) (u = s[f]) && (d = e.call(u, u.__data__, f, s)) && ("__data__" in u && (d.__data__ = u.__data__), l[f] = d, Wr(l[f], t, n, f, l, qr(u, n)));
	return new Qi(a, this._parents, t, n);
}
// node_modules/d3-transition/src/transition/selectAll.js
function ji(e) {
	var t = this._name, n = this._id;
	typeof e != "function" && (e = re(e));
	for (var r = this._groups, i = r.length, a = [], o = [], s = 0; s < i; ++s) for (var c = r[s], l = c.length, u, d = 0; d < l; ++d) if (u = c[d]) {
		for (var f = e.call(u, u.__data__, d, c), p, m = qr(u, n), h = 0, g = f.length; h < g; ++h) (p = f[h]) && Wr(p, t, n, h, f, m);
		a.push(f), o.push(u);
	}
	return new Qi(a, o, t, n);
}
// node_modules/d3-transition/src/transition/selection.js
var Mi = zt.prototype.constructor;
function Ni() {
	return new Mi(this._groups, this._parents);
}
// node_modules/d3-transition/src/transition/style.js
function Pi(e, t) {
	var n, r, i;
	return function() {
		var a = Ke(this, e), o = (this.style.removeProperty(e), Ke(this, e));
		return a === o ? null : a === n && o === r ? i : i = t(n = a, r = o);
	};
}
function Fi(e) {
	return function() {
		this.style.removeProperty(e);
	};
}
function Ii(e, t, n) {
	var r, i = n + "", a;
	return function() {
		var o = Ke(this, e);
		return o === i ? null : o === r ? a : a = t(r = o, n);
	};
}
function Li(e, t, n) {
	var r, i, a;
	return function() {
		var o = Ke(this, e), s = n(this), c = s + "";
		return s ?? (c = s = (this.style.removeProperty(e), Ke(this, e))), o === c ? null : o === r && c === i ? a : (i = c, a = t(r = o, s));
	};
}
function Ri(e, t) {
	var n, r, i, a = "style." + t, o = "end." + a, s;
	return function() {
		var c = Kr(this, e), l = c.on, u = c.value[a] == null ? s ||= Fi(t) : void 0;
		(l !== n || i !== u) && (r = (n = l).copy()).on(o, i = u), c.on = r;
	};
}
function zi(e, t, n) {
	var r = (e += "") == "transform" ? mr : ti;
	return t == null ? this.styleTween(e, Pi(e, r)).on("end.style." + e, Fi(e)) : typeof t == "function" ? this.styleTween(e, Li(e, r, ei(this, "style." + e, t))).each(Ri(this._id, e)) : this.styleTween(e, Ii(e, r, t), n).on("end.style." + e, null);
}
// node_modules/d3-transition/src/transition/styleTween.js
function Bi(e, t, n) {
	return function(r) {
		this.style.setProperty(e, t.call(this, r), n);
	};
}
function Vi(e, t, n) {
	var r, i;
	function a() {
		var a = t.apply(this, arguments);
		return a !== i && (r = (i = a) && Bi(e, a, n)), r;
	}
	return a._value = t, a;
}
function Hi(e, t, n) {
	var r = "style." + (e += "");
	if (arguments.length < 2) return (r = this.tween(r)) && r._value;
	if (t == null) return this.tween(r, null);
	if (typeof t != "function") throw Error();
	return this.tween(r, Vi(e, t, n ?? ""));
}
// node_modules/d3-transition/src/transition/text.js
function Ui(e) {
	return function() {
		this.textContent = e;
	};
}
function Wi(e) {
	return function() {
		var t = e(this);
		this.textContent = t ?? "";
	};
}
function Gi(e) {
	return this.tween("text", typeof e == "function" ? Wi(ei(this, "text", e)) : Ui(e == null ? "" : e + ""));
}
// node_modules/d3-transition/src/transition/textTween.js
function Ki(e) {
	return function(t) {
		this.textContent = e.call(this, t);
	};
}
function qi(e) {
	var t, n;
	function r() {
		var r = e.apply(this, arguments);
		return r !== n && (t = (n = r) && Ki(r)), t;
	}
	return r._value = e, r;
}
function Ji(e) {
	var t = "text";
	if (arguments.length < 1) return (t = this.tween(t)) && t._value;
	if (e == null) return this.tween(t, null);
	if (typeof e != "function") throw Error();
	return this.tween(t, qi(e));
}
// node_modules/d3-transition/src/transition/transition.js
function Yi() {
	for (var e = this._name, t = this._id, n = $i(), r = this._groups, i = r.length, a = 0; a < i; ++a) for (var o = r[a], s = o.length, c, l = 0; l < s; ++l) if (c = o[l]) {
		var u = qr(c, t);
		Wr(c, e, n, l, o, {
			time: u.time + u.delay + u.duration,
			delay: 0,
			duration: u.duration,
			ease: u.ease
		});
	}
	return new Qi(r, this._parents, e, n);
}
// node_modules/d3-transition/src/transition/end.js
function Xi() {
	var e, t, n = this, r = n._id, i = n.size();
	return new Promise(function(a, o) {
		var s = { value: o }, c = { value: function() {
			--i === 0 && a();
		} };
		n.each(function() {
			var n = Kr(this, r), i = n.on;
			i !== e && (t = (e = i).copy(), t._.cancel.push(s), t._.interrupt.push(s), t._.end.push(c)), n.on = t;
		}), i === 0 && a();
	});
}
// node_modules/d3-transition/src/transition/index.js
var Zi = 0;
function Qi(e, t, n, r) {
	this._groups = e, this._parents = t, this._name = n, this._id = r;
}
function $i() {
	return ++Zi;
}
var ea = zt.prototype;
Qi.prototype = {
	constructor: Qi,
	select: Ai,
	selectAll: ji,
	selectChild: ea.selectChild,
	selectChildren: ea.selectChildren,
	filter: wi,
	merge: Ti,
	selection: Ni,
	transition: Yi,
	call: ea.call,
	nodes: ea.nodes,
	node: ea.node,
	size: ea.size,
	empty: ea.empty,
	each: ea.each,
	on: Oi,
	attr: ci,
	attrTween: pi,
	style: zi,
	styleTween: Hi,
	text: Gi,
	textTween: Ji,
	remove: ki,
	tween: $r,
	delay: gi,
	duration: yi,
	ease: xi,
	easeVarying: Ci,
	end: Xi,
	[Symbol.iterator]: ea[Symbol.iterator]
};
// node_modules/d3-ease/src/cubic.js
function ta(e) {
	return ((e *= 2) <= 1 ? e * e * e : (e -= 2) * e * e + 2) / 2;
}
// node_modules/d3-transition/src/selection/transition.js
var na = {
	time: null,
	delay: 0,
	duration: 250,
	ease: ta
};
function ra(e, t) {
	for (var n; !(n = e.__transition) || !(n = n[t]);) if (!(e = e.parentNode)) throw Error(`transition ${t} not found`);
	return n;
}
function ia(e) {
	var t, n;
	e instanceof Qi ? (t = e._id, e = e._name) : (t = $i(), (n = na).time = Mr(), e = e == null ? null : e + "");
	for (var r = this._groups, i = r.length, a = 0; a < i; ++a) for (var o = r[a], s = o.length, c, l = 0; l < s; ++l) (c = o[l]) && Wr(c, e, t, l, o, n || ra(c, t));
	return new Qi(r, this._parents, e, t);
}
zt.prototype.interrupt = Xr, zt.prototype.transition = ia;
// node_modules/d3-zoom/src/constant.js
var aa = (e) => () => e;
// node_modules/d3-zoom/src/event.js
function oa(e, { sourceEvent: t, target: n, transform: r, dispatch: i }) {
	Object.defineProperties(this, {
		type: {
			value: e,
			enumerable: !0,
			configurable: !0
		},
		sourceEvent: {
			value: t,
			enumerable: !0,
			configurable: !0
		},
		target: {
			value: n,
			enumerable: !0,
			configurable: !0
		},
		transform: {
			value: r,
			enumerable: !0,
			configurable: !0
		},
		_: { value: i }
	});
}
// node_modules/d3-zoom/src/transform.js
function sa(e, t, n) {
	this.k = e, this.x = t, this.y = n;
}
sa.prototype = {
	constructor: sa,
	scale: function(e) {
		return e === 1 ? this : new sa(this.k * e, this.x, this.y);
	},
	translate: function(e, t) {
		return e === 0 & t === 0 ? this : new sa(this.k, this.x + this.k * e, this.y + this.k * t);
	},
	apply: function(e) {
		return [e[0] * this.k + this.x, e[1] * this.k + this.y];
	},
	applyX: function(e) {
		return e * this.k + this.x;
	},
	applyY: function(e) {
		return e * this.k + this.y;
	},
	invert: function(e) {
		return [(e[0] - this.x) / this.k, (e[1] - this.y) / this.k];
	},
	invertX: function(e) {
		return (e - this.x) / this.k;
	},
	invertY: function(e) {
		return (e - this.y) / this.k;
	},
	rescaleX: function(e) {
		return e.copy().domain(e.range().map(this.invertX, this).map(e.invert, e));
	},
	rescaleY: function(e) {
		return e.copy().domain(e.range().map(this.invertY, this).map(e.invert, e));
	},
	toString: function() {
		return "translate(" + this.x + "," + this.y + ") scale(" + this.k + ")";
	}
};
var ca = new sa(1, 0, 0);
la.prototype = sa.prototype;
function la(e) {
	for (; !e.__zoom;) if (!(e = e.parentNode)) return ca;
	return e.__zoom;
}
// node_modules/d3-zoom/src/noevent.js
function ua(e) {
	e.stopImmediatePropagation();
}
function da(e) {
	e.preventDefault(), e.stopImmediatePropagation();
}
// node_modules/d3-zoom/src/zoom.js
function fa(e) {
	return (!e.ctrlKey || e.type === "wheel") && !e.button;
}
function pa() {
	var e = this;
	return e instanceof SVGElement ? (e = e.ownerSVGElement || e, e.hasAttribute("viewBox") ? (e = e.viewBox.baseVal, [[e.x, e.y], [e.x + e.width, e.y + e.height]]) : [[0, 0], [e.width.baseVal.value, e.height.baseVal.value]]) : [[0, 0], [e.clientWidth, e.clientHeight]];
}
function ma() {
	return this.__zoom || ca;
}
function ha(e) {
	return -e.deltaY * (e.deltaMode === 1 ? .05 : e.deltaMode ? 1 : .002) * (e.ctrlKey ? 10 : 1);
}
function ga() {
	return navigator.maxTouchPoints || "ontouchstart" in this;
}
function _a(e, t, n) {
	var r = e.invertX(t[0][0]) - n[0][0], i = e.invertX(t[1][0]) - n[1][0], a = e.invertY(t[0][1]) - n[0][1], o = e.invertY(t[1][1]) - n[1][1];
	return e.translate(i > r ? (r + i) / 2 : Math.min(0, r) || Math.max(0, i), o > a ? (a + o) / 2 : Math.min(0, a) || Math.max(0, o));
}
function va() {
	var e = fa, t = pa, n = _a, r = ha, i = ga, a = [0, Infinity], o = [[-Infinity, -Infinity], [Infinity, Infinity]], s = 250, c = br, l = w("start", "zoom", "end"), u, d, f, p = 500, m = 150, h = 0, g = 10;
	function _(e) {
		e.property("__zoom", ma).on("wheel.zoom", T, { passive: !1 }).on("mousedown.zoom", E).on("dblclick.zoom", D).filter(i).on("touchstart.zoom", O).on("touchmove.zoom", k).on("touchend.zoom touchcancel.zoom", A).style("-webkit-tap-highlight-color", "rgba(0,0,0,0)");
	}
	_.transform = function(e, t, n, r) {
		var i = e.selection ? e.selection() : e;
		i.property("__zoom", ma), e === i ? i.interrupt().each(function() {
			S(this, arguments).event(r).start().zoom(null, typeof t == "function" ? t.apply(this, arguments) : t).end();
		}) : x(e, t, n, r);
	}, _.scaleBy = function(e, t, n, r) {
		_.scaleTo(e, function() {
			return this.__zoom.k * (typeof t == "function" ? t.apply(this, arguments) : t);
		}, n, r);
	}, _.scaleTo = function(e, r, i, a) {
		_.transform(e, function() {
			var e = t.apply(this, arguments), a = this.__zoom, s = i == null ? b(e) : typeof i == "function" ? i.apply(this, arguments) : i, c = a.invert(s), l = typeof r == "function" ? r.apply(this, arguments) : r;
			return n(y(v(a, l), s, c), e, o);
		}, i, a);
	}, _.translateBy = function(e, r, i, a) {
		_.transform(e, function() {
			return n(this.__zoom.translate(typeof r == "function" ? r.apply(this, arguments) : r, typeof i == "function" ? i.apply(this, arguments) : i), t.apply(this, arguments), o);
		}, null, a);
	}, _.translateTo = function(e, r, i, a, s) {
		_.transform(e, function() {
			var e = t.apply(this, arguments), s = this.__zoom, c = a == null ? b(e) : typeof a == "function" ? a.apply(this, arguments) : a;
			return n(ca.translate(c[0], c[1]).scale(s.k).translate(typeof r == "function" ? -r.apply(this, arguments) : -r, typeof i == "function" ? -i.apply(this, arguments) : -i), e, o);
		}, a, s);
	};
	function v(e, t) {
		return t = Math.max(a[0], Math.min(a[1], t)), t === e.k ? e : new sa(t, e.x, e.y);
	}
	function y(e, t, n) {
		var r = t[0] - n[0] * e.k, i = t[1] - n[1] * e.k;
		return r === e.x && i === e.y ? e : new sa(e.k, r, i);
	}
	function b(e) {
		return [(+e[0][0] + +e[1][0]) / 2, (+e[0][1] + +e[1][1]) / 2];
	}
	function x(e, n, r, i) {
		e.on("start.zoom", function() {
			S(this, arguments).event(i).start();
		}).on("interrupt.zoom end.zoom", function() {
			S(this, arguments).event(i).end();
		}).tween("zoom", function() {
			var e = this, a = arguments, o = S(e, a).event(i), s = t.apply(e, a), l = r == null ? b(s) : typeof r == "function" ? r.apply(e, a) : r, u = Math.max(s[1][0] - s[0][0], s[1][1] - s[0][1]), d = e.__zoom, f = typeof n == "function" ? n.apply(e, a) : n, p = c(d.invert(l).concat(u / d.k), f.invert(l).concat(u / f.k));
			return function(e) {
				if (e === 1) e = f;
				else {
					var t = p(e), n = u / t[2];
					e = new sa(n, l[0] - t[0] * n, l[1] - t[1] * n);
				}
				o.zoom(null, e);
			};
		});
	}
	function S(e, t, n) {
		return !n && e.__zooming || new C(e, t);
	}
	function C(e, n) {
		this.that = e, this.args = n, this.active = 0, this.sourceEvent = null, this.extent = t.apply(e, n), this.taps = 0;
	}
	C.prototype = {
		event: function(e) {
			return e && (this.sourceEvent = e), this;
		},
		start: function() {
			return ++this.active === 1 && (this.that.__zooming = this, this.emit("start")), this;
		},
		zoom: function(e, t) {
			return this.mouse && e !== "mouse" && (this.mouse[1] = t.invert(this.mouse[0])), this.touch0 && e !== "touch" && (this.touch0[1] = t.invert(this.touch0[0])), this.touch1 && e !== "touch" && (this.touch1[1] = t.invert(this.touch1[0])), this.that.__zoom = t, this.emit("zoom"), this;
		},
		end: function() {
			return --this.active === 0 && (delete this.that.__zooming, this.emit("end")), this;
		},
		emit: function(e) {
			var t = Vt(this.that).datum();
			l.call(e, this.that, new oa(e, {
				sourceEvent: this.sourceEvent,
				target: _,
				type: e,
				transform: this.that.__zoom,
				dispatch: l
			}), t);
		}
	};
	function T(t, ...i) {
		if (!e.apply(this, arguments)) return;
		var s = S(this, i).event(t), c = this.__zoom, l = Math.max(a[0], Math.min(a[1], c.k * 2 ** r.apply(this, arguments))), u = Ut(t);
		if (s.wheel) (s.mouse[0][0] !== u[0] || s.mouse[0][1] !== u[1]) && (s.mouse[1] = c.invert(s.mouse[0] = u)), clearTimeout(s.wheel);
		else if (c.k === l) return;
		else s.mouse = [u, c.invert(u)], Yr(this), s.start();
		da(t), s.wheel = setTimeout(d, m), s.zoom("mouse", n(y(v(c, l), s.mouse[0], s.mouse[1]), s.extent, o));
		function d() {
			s.wheel = null, s.end();
		}
	}
	function E(t, ...r) {
		if (f || !e.apply(this, arguments)) return;
		var i = t.currentTarget, a = S(this, r, !0).event(t), s = Vt(t.view).on("mousemove.zoom", d, !0).on("mouseup.zoom", p, !0), c = Ut(t, i), l = t.clientX, u = t.clientY;
		Jt(t.view), ua(t), a.mouse = [c, this.__zoom.invert(c)], Yr(this), a.start();
		function d(e) {
			if (da(e), !a.moved) {
				var t = e.clientX - l, r = e.clientY - u;
				a.moved = t * t + r * r > h;
			}
			a.event(e).zoom("mouse", n(y(a.that.__zoom, a.mouse[0] = Ut(e, i), a.mouse[1]), a.extent, o));
		}
		function p(e) {
			s.on("mousemove.zoom mouseup.zoom", null), Yt(e.view, a.moved), da(e), a.event(e).end();
		}
	}
	function D(r, ...i) {
		if (e.apply(this, arguments)) {
			var a = this.__zoom, c = Ut(r.changedTouches ? r.changedTouches[0] : r, this), l = a.invert(c), u = a.k * (r.shiftKey ? .5 : 2), d = n(y(v(a, u), c, l), t.apply(this, i), o);
			da(r), s > 0 ? Vt(this).transition().duration(s).call(x, d, c, r) : Vt(this).call(_.transform, d, c, r);
		}
	}
	function O(t, ...n) {
		if (e.apply(this, arguments)) {
			var r = t.touches, i = r.length, a = S(this, n, t.changedTouches.length === i).event(t), o, s, c, l;
			for (ua(t), s = 0; s < i; ++s) c = r[s], l = Ut(c, this), l = [
				l,
				this.__zoom.invert(l),
				c.identifier
			], a.touch0 ? !a.touch1 && a.touch0[2] !== l[2] && (a.touch1 = l, a.taps = 0) : (a.touch0 = l, o = !0, a.taps = 1 + !!u);
			u &&= clearTimeout(u), o && (a.taps < 2 && (d = l[0], u = setTimeout(function() {
				u = null;
			}, p)), Yr(this), a.start());
		}
	}
	function k(e, ...t) {
		if (this.__zooming) {
			var r = S(this, t).event(e), i = e.changedTouches, a = i.length, s, c, l, u;
			for (da(e), s = 0; s < a; ++s) c = i[s], l = Ut(c, this), r.touch0 && r.touch0[2] === c.identifier ? r.touch0[0] = l : r.touch1 && r.touch1[2] === c.identifier && (r.touch1[0] = l);
			if (c = r.that.__zoom, r.touch1) {
				var d = r.touch0[0], f = r.touch0[1], p = r.touch1[0], m = r.touch1[1], h = (h = p[0] - d[0]) * h + (h = p[1] - d[1]) * h, g = (g = m[0] - f[0]) * g + (g = m[1] - f[1]) * g;
				c = v(c, Math.sqrt(h / g)), l = [(d[0] + p[0]) / 2, (d[1] + p[1]) / 2], u = [(f[0] + m[0]) / 2, (f[1] + m[1]) / 2];
			} else if (r.touch0) l = r.touch0[0], u = r.touch0[1];
			else return;
			r.zoom("touch", n(y(c, l, u), r.extent, o));
		}
	}
	function A(e, ...t) {
		if (this.__zooming) {
			var n = S(this, t).event(e), r = e.changedTouches, i = r.length, a, o;
			for (ua(e), f && clearTimeout(f), f = setTimeout(function() {
				f = null;
			}, p), a = 0; a < i; ++a) o = r[a], n.touch0 && n.touch0[2] === o.identifier ? delete n.touch0 : n.touch1 && n.touch1[2] === o.identifier && delete n.touch1;
			if (n.touch1 && !n.touch0 && (n.touch0 = n.touch1, delete n.touch1), n.touch0) n.touch0[1] = this.__zoom.invert(n.touch0[0]);
			else if (n.end(), n.taps === 2 && (o = Ut(o, this), Math.hypot(d[0] - o[0], d[1] - o[1]) < g)) {
				var s = Vt(this).on("dblclick.zoom");
				s && s.apply(this, arguments);
			}
		}
	}
	return _.wheelDelta = function(e) {
		return arguments.length ? (r = typeof e == "function" ? e : aa(+e), _) : r;
	}, _.filter = function(t) {
		return arguments.length ? (e = typeof t == "function" ? t : aa(!!t), _) : e;
	}, _.touchable = function(e) {
		return arguments.length ? (i = typeof e == "function" ? e : aa(!!e), _) : i;
	}, _.extent = function(e) {
		return arguments.length ? (t = typeof e == "function" ? e : aa([[+e[0][0], +e[0][1]], [+e[1][0], +e[1][1]]]), _) : t;
	}, _.scaleExtent = function(e) {
		return arguments.length ? (a[0] = +e[0], a[1] = +e[1], _) : [a[0], a[1]];
	}, _.translateExtent = function(e) {
		return arguments.length ? (o[0][0] = +e[0][0], o[1][0] = +e[1][0], o[0][1] = +e[0][1], o[1][1] = +e[1][1], _) : [[o[0][0], o[0][1]], [o[1][0], o[1][1]]];
	}, _.constrain = function(e) {
		return arguments.length ? (n = e, _) : n;
	}, _.duration = function(e) {
		return arguments.length ? (s = +e, _) : s;
	}, _.interpolate = function(e) {
		return arguments.length ? (c = e, _) : c;
	}, _.on = function() {
		var e = l.on.apply(l, arguments);
		return e === l ? _ : e;
	}, _.clickDistance = function(e) {
		return arguments.length ? (h = (e = +e) * e, _) : Math.sqrt(h);
	}, _.tapDistance = function(e) {
		return arguments.length ? (g = +e, _) : g;
	}, _;
}
// node_modules/@xyflow/system/dist/esm/index.js
var ya = {
	error001: (e = "react") => `Seems like you have not used ${e === "svelte" ? "SvelteFlowProvider" : "ReactFlowProvider"} as an ancestor. Help: https://${e}flow.dev/error#001`,
	error002: () => "It looks like you've created a new nodeTypes or edgeTypes object. If this wasn't on purpose please define the nodeTypes/edgeTypes outside of the component or memoize them.",
	error003: (e) => `Node type "${e}" not found. Using fallback type "default".`,
	error004: () => "The parent container needs a width and a height to render the graph.",
	error005: () => "Only child nodes can use a parent extent.",
	error006: () => "Can't create edge. An edge needs a source and a target.",
	error007: (e) => `The old edge with id=${e} does not exist.`,
	error009: (e) => `Marker type "${e}" doesn't exist.`,
	error008: (e, { id: t, sourceHandle: n, targetHandle: r }) => `Couldn't create edge for ${e} handle id: "${e === "source" ? n : r}", edge id: ${t}.`,
	error010: () => "Handle: No node id found. Make sure to only use a Handle inside a custom Node.",
	error011: (e) => `Edge type "${e}" not found. Using fallback type "default".`,
	error012: (e) => `Node with id "${e}" does not exist, it may have been removed. This can happen when a node is deleted before the "onNodeClick" handler is called.`,
	error013: (e = "react") => `It seems that you haven't loaded the styles. Please import '@xyflow/${e}/dist/style.css' or base.css to make sure everything is working properly.`,
	error014: () => "useNodeConnections: No node ID found. Call useNodeConnections inside a custom Node or provide a node ID.",
	error015: () => "It seems that you are trying to drag a node that is not initialized. Please use onNodesChange as explained in the docs.",
	error016: (e) => `Edge with id "${e}" does not exist, it may have been removed. This can happen when an edge is deleted before the "onEdgeClick" handler is called.`
}, ba = [[-Infinity, -Infinity], [Infinity, Infinity]], xa = [
	"Enter",
	" ",
	"Escape"
], Sa = {
	"node.a11yDescription.default": "Press enter or space to select a node. Press delete to remove it and escape to cancel.",
	"node.a11yDescription.keyboardDisabled": "Press enter or space to select a node. You can then use the arrow keys to move the node around. Press delete to remove it and escape to cancel.",
	"node.a11yDescription.ariaLiveMessage": ({ direction: e, x: t, y: n }) => `Moved selected node ${e}. New position, x: ${t}, y: ${n}`,
	"edge.a11yDescription.default": "Press enter or space to select an edge. You can then press delete to remove it or escape to cancel.",
	"controls.ariaLabel": "Control Panel",
	"controls.zoomIn.ariaLabel": "Zoom In",
	"controls.zoomOut.ariaLabel": "Zoom Out",
	"controls.fitView.ariaLabel": "Fit View",
	"controls.interactive.ariaLabel": "Toggle Interactivity",
	"minimap.ariaLabel": "Mini Map",
	"handle.ariaLabel": "Handle"
}, Ca;
(function(e) {
	e.Strict = "strict", e.Loose = "loose";
})(Ca ||= {});
var wa;
(function(e) {
	e.Free = "free", e.Vertical = "vertical", e.Horizontal = "horizontal";
})(wa ||= {});
var Ta;
(function(e) {
	e.Partial = "partial", e.Full = "full";
})(Ta ||= {});
var Ea = {
	inProgress: !1,
	isValid: null,
	from: null,
	fromHandle: null,
	fromPosition: null,
	fromNode: null,
	to: null,
	toHandle: null,
	toPosition: null,
	toNode: null,
	pointer: null
}, Da;
(function(e) {
	e.Bezier = "default", e.Straight = "straight", e.Step = "step", e.SmoothStep = "smoothstep", e.SimpleBezier = "simplebezier";
})(Da ||= {});
var Oa;
(function(e) {
	e.Arrow = "arrow", e.ArrowClosed = "arrowclosed";
})(Oa ||= {});
var V;
(function(e) {
	e.Left = "left", e.Top = "top", e.Right = "right", e.Bottom = "bottom";
})(V ||= {});
var ka = {
	[V.Left]: V.Right,
	[V.Right]: V.Left,
	[V.Top]: V.Bottom,
	[V.Bottom]: V.Top
}, Aa = (e) => !!e && typeof e == "object" && "id" in e && "source" in e && "target" in e, ja = (e) => !!e && typeof e == "object" && "id" in e && "position" in e && !("source" in e) && !("target" in e), Ma = (e) => !!e && typeof e == "object" && "id" in e && "internals" in e && !("source" in e) && !("target" in e), Na = (e, t = [0, 0]) => {
	let { width: n, height: r } = fo(e), i = e.origin ?? t, a = n * i[0], o = r * i[1];
	return {
		x: e.position.x - a,
		y: e.position.y - o
	};
}, Pa = (e, t = { nodeOrigin: [0, 0] }) => {
	if (e.length === 0) return {
		x: 0,
		y: 0,
		width: 0,
		height: 0
	};
	let n = !1, r = e.reduce((e, r) => {
		let i = typeof r == "string", a = !t.nodeLookup && !i ? r : void 0;
		return t.nodeLookup && (a = i ? t.nodeLookup.get(r) : Ma(r) ? r : t.nodeLookup.get(r.id)), a ? (n = !0, Ga(e, Ya(a, t.nodeOrigin))) : e;
	}, {
		x: Infinity,
		y: Infinity,
		x2: -Infinity,
		y2: -Infinity
	});
	return n ? qa(r) : {
		x: 0,
		y: 0,
		width: 0,
		height: 0
	};
}, Fa = (e, t = {}) => {
	let n = {
		x: Infinity,
		y: Infinity,
		x2: -Infinity,
		y2: -Infinity
	}, r = !1;
	return e.forEach((e) => {
		(t.filter === void 0 || t.filter(e)) && (n = Ga(n, Ya(e)), r = !0);
	}), r ? qa(n) : {
		x: 0,
		y: 0,
		width: 0,
		height: 0
	};
}, Ia = (e, t, [n, r, i] = [
	0,
	0,
	1
], a = !1, o = !1) => {
	let s = (t.x - n) / i, c = (t.y - r) / i, l = t.width / i, u = t.height / i, d = [];
	for (let t of e.values()) {
		let { measured: e, selectable: n = !0, hidden: r = !1 } = t;
		if (o && !n || r) continue;
		let i = e.width ?? t.width ?? t.initialWidth ?? 0, f = e.height ?? t.height ?? t.initialHeight ?? 0, { x: p, y: m } = t.internals.positionAbsolute, h = Za(s, c, l, u, p, m, i, f), g = i * f, _ = a && h > 0;
		(!t.internals.handleBounds || _ || h >= g || t.dragging) && d.push(t);
	}
	return d;
}, La = (e, t) => {
	let n = /* @__PURE__ */ new Set();
	return e.forEach((e) => {
		n.add(e.id);
	}), t.filter((e) => n.has(e.source) || n.has(e.target));
};
function Ra(e, t) {
	let n = /* @__PURE__ */ new Map(), r = t?.nodes ? new Set(t.nodes.map((e) => e.id)) : null;
	return e.forEach((e) => {
		let i;
		if (t?.includeHiddenNodes) {
			let { width: t, height: n } = fo(e);
			i = t > 0 && n > 0;
		} else i = !!(e.measured.width && e.measured.height && !e.hidden);
		i && (!r || r.has(e.id)) && n.set(e.id, e);
	}), n;
}
async function za({ nodes: e, width: t, height: n, panZoom: r, minZoom: i, maxZoom: a }, o) {
	if (e.size === 0) return !0;
	let s = co(Fa(Ra(e, o)), t, n, o?.minZoom ?? i, o?.maxZoom ?? a, o?.padding ?? .1);
	return await r.setViewport(s, {
		duration: o?.duration,
		ease: o?.ease,
		interpolate: o?.interpolate
	}), !0;
}
function Ba({ nodeId: e, nextPosition: t, nodeLookup: n, nodeOrigin: r = [0, 0], nodeExtent: i, onError: a }) {
	let o = n.get(e), s = o.parentId ? n.get(o.parentId) : void 0, { x: c, y: l } = s ? s.internals.positionAbsolute : {
		x: 0,
		y: 0
	}, u = o.origin ?? r, d = o.extent || i;
	if (o.extent === "parent" && !o.expandParent) {
		if (!s) a?.("005", ya.error005());
		else {
			let { width: e, height: t } = fo(s);
			e && t && (d = [[c, l], [c + e, l + t]]);
		}
	} else s && uo(o.extent) && (d = [[o.extent[0][0] + c, o.extent[0][1] + l], [o.extent[1][0] + c, o.extent[1][1] + l]]);
	let f = uo(d) ? H(t, d, o.measured) : t;
	return (o.measured.width === void 0 || o.measured.height === void 0) && a?.("015", ya.error015()), {
		position: {
			x: f.x - c + (o.measured.width ?? 0) * u[0],
			y: f.y - l + (o.measured.height ?? 0) * u[1]
		},
		positionAbsolute: f
	};
}
async function Va({ nodesToRemove: e = [], edgesToRemove: t = [], nodes: n, edges: r, onBeforeDelete: i }) {
	let a = new Set(e.map((e) => e.id)), o = [];
	for (let e of n) {
		if (e.deletable === !1) continue;
		let t = a.has(e.id), n = !t && e.parentId && o.find((t) => t.id === e.parentId);
		(t || n) && o.push(e);
	}
	let s = new Set(t.map((e) => e.id)), c = r.filter((e) => e.deletable !== !1), l = La(o, c);
	for (let e of c) s.has(e.id) && !l.find((t) => t.id === e.id) && l.push(e);
	if (!i) return {
		edges: l,
		nodes: o
	};
	let u = await i({
		nodes: o,
		edges: l
	});
	return typeof u == "boolean" ? u ? {
		edges: l,
		nodes: o
	} : {
		edges: [],
		nodes: []
	} : u;
}
var Ha = (e, t = 0, n = 1) => Math.min(Math.max(e, t), n), H = (e = {
	x: 0,
	y: 0
}, t, n) => ({
	x: Ha(e.x, t[0][0], t[1][0] - (n?.width ?? 0)),
	y: Ha(e.y, t[0][1], t[1][1] - (n?.height ?? 0))
});
function U(e, t, n) {
	let { width: r, height: i } = fo(n), { x: a, y: o } = n.internals.positionAbsolute;
	return H(e, [[a, o], [a + r, o + i]], t);
}
var Ua = (e, t, n) => e < t ? Ha(Math.abs(e - t), 1, t) / t : e > n ? -Ha(Math.abs(e - n), 1, t) / t : 0, Wa = (e, t, n = 15, r = 40) => [Ua(e.x, r, t.width - r) * n, Ua(e.y, r, t.height - r) * n], Ga = (e, t) => ({
	x: Math.min(e.x, t.x),
	y: Math.min(e.y, t.y),
	x2: Math.max(e.x2, t.x2),
	y2: Math.max(e.y2, t.y2)
}), Ka = ({ x: e, y: t, width: n, height: r }) => ({
	x: e,
	y: t,
	x2: e + n,
	y2: t + r
}), qa = ({ x: e, y: t, x2: n, y2: r }) => ({
	x: e,
	y: t,
	width: n - e,
	height: r - t
}), Ja = (e, t = [0, 0]) => {
	let { x: n, y: r } = Ma(e) ? e.internals.positionAbsolute : Na(e, t);
	return {
		x: n,
		y: r,
		width: e.measured?.width ?? e.width ?? e.initialWidth ?? 0,
		height: e.measured?.height ?? e.height ?? e.initialHeight ?? 0
	};
}, Ya = (e, t = [0, 0]) => {
	let { x: n, y: r } = Ma(e) ? e.internals.positionAbsolute : Na(e, t);
	return {
		x: n,
		y: r,
		x2: n + (e.measured?.width ?? e.width ?? e.initialWidth ?? 0),
		y2: r + (e.measured?.height ?? e.height ?? e.initialHeight ?? 0)
	};
}, Xa = (e, t) => qa(Ga(Ka(e), Ka(t))), Za = (e, t, n, r, i, a, o, s) => {
	let c = Math.max(0, Math.min(e + n, i + o) - Math.max(e, i)), l = Math.max(0, Math.min(t + r, a + s) - Math.max(t, a));
	return Math.ceil(c * l);
}, Qa = (e, t) => Za(e.x, e.y, e.width, e.height, t.x, t.y, t.width, t.height), $a = (e) => eo(e.width) && eo(e.height) && eo(e.x) && eo(e.y), eo = (e) => !isNaN(e) && isFinite(e), to = (e, t) => (e, t) => {}, no = (e, t = [1, 1]) => ({
	x: t[0] * Math.round(e.x / t[0]),
	y: t[1] * Math.round(e.y / t[1])
}), ro = ({ x: e, y: t }, [n, r, i], a = !1, o = [1, 1]) => {
	let s = {
		x: (e - n) / i,
		y: (t - r) / i
	};
	return a ? no(s, o) : s;
}, io = ({ x: e, y: t }, [n, r, i]) => ({
	x: e * i + n,
	y: t * i + r
});
function ao(e, t) {
	if (typeof e == "number") return Math.floor((t - t / (1 + e)) * .5);
	if (typeof e == "string" && e.endsWith("px")) {
		let t = parseFloat(e);
		if (!Number.isNaN(t)) return Math.floor(t);
	}
	if (typeof e == "string" && e.endsWith("%")) {
		let n = parseFloat(e);
		if (!Number.isNaN(n)) return Math.floor(t * n * .01);
	}
	return console.error(`The padding value "${e}" is invalid. Please provide a number or a string with a valid unit (px or %).`), 0;
}
function oo(e, t, n) {
	if (typeof e == "string" || typeof e == "number") {
		let r = ao(e, n), i = ao(e, t);
		return {
			top: r,
			right: i,
			bottom: r,
			left: i,
			x: i * 2,
			y: r * 2
		};
	}
	if (typeof e == "object") {
		let r = ao(e.top ?? e.y ?? 0, n), i = ao(e.bottom ?? e.y ?? 0, n), a = ao(e.left ?? e.x ?? 0, t), o = ao(e.right ?? e.x ?? 0, t);
		return {
			top: r,
			right: o,
			bottom: i,
			left: a,
			x: a + o,
			y: r + i
		};
	}
	return {
		top: 0,
		right: 0,
		bottom: 0,
		left: 0,
		x: 0,
		y: 0
	};
}
function so(e, t, n, r, i, a) {
	let { x: o, y: s } = io(e, [
		t,
		n,
		r
	]), { x: c, y: l } = io({
		x: e.x + e.width,
		y: e.y + e.height
	}, [
		t,
		n,
		r
	]), u = i - c, d = a - l;
	return {
		left: Math.floor(o),
		top: Math.floor(s),
		right: Math.floor(u),
		bottom: Math.floor(d)
	};
}
var co = (e, t, n, r, i, a) => {
	let o = oo(a, t, n), s = (t - o.x) / e.width, c = (n - o.y) / e.height, l = Ha(Math.min(s, c), r, i), u = e.x + e.width / 2, d = e.y + e.height / 2, f = t / 2 - u * l, p = n / 2 - d * l, m = so(e, f, p, l, t, n), h = {
		left: Math.min(m.left - o.left, 0),
		top: Math.min(m.top - o.top, 0),
		right: Math.min(m.right - o.right, 0),
		bottom: Math.min(m.bottom - o.bottom, 0)
	};
	return {
		x: f - h.left + h.right,
		y: p - h.top + h.bottom,
		zoom: l
	};
}, lo = () => typeof navigator < "u" && navigator?.userAgent?.indexOf("Mac") >= 0;
function uo(e) {
	return e != null && e !== "parent";
}
function fo(e) {
	return {
		width: e.measured?.width ?? e.width ?? e.initialWidth ?? 0,
		height: e.measured?.height ?? e.height ?? e.initialHeight ?? 0
	};
}
function po(e) {
	return (e.measured?.width ?? e.width ?? e.initialWidth) !== void 0 && (e.measured?.height ?? e.height ?? e.initialHeight) !== void 0;
}
function mo(e, t = {
	width: 0,
	height: 0
}, n, r, i) {
	let a = { ...e }, o = r.get(n);
	if (o) {
		let e = o.origin || i;
		a.x += o.internals.positionAbsolute.x - (t.width ?? 0) * e[0], a.y += o.internals.positionAbsolute.y - (t.height ?? 0) * e[1];
	}
	return a;
}
function ho(e, t) {
	if (e.size !== t.size) return !1;
	for (let n of e) if (!t.has(n)) return !1;
	return !0;
}
function go() {
	let e, t;
	return {
		promise: new Promise((n, r) => {
			e = n, t = r;
		}),
		resolve: e,
		reject: t
	};
}
function _o(e) {
	return {
		...Sa,
		...e || {}
	};
}
function vo(e) {
	return e === null ? null : e ? "valid" : "invalid";
}
function yo(e, { snapGrid: t = [0, 0], snapToGrid: n = !1, transform: r, containerBounds: i }) {
	let { x: a, y: o } = To(e), s = ro({
		x: a - (i?.left ?? 0),
		y: o - (i?.top ?? 0)
	}, r), { x: c, y: l } = n ? no(s, t) : s;
	return {
		xSnapped: c,
		ySnapped: l,
		...s
	};
}
var bo = (e) => ({
	width: e.offsetWidth,
	height: e.offsetHeight
}), xo = (e) => e?.getRootNode?.() || window?.document, So = [
	"INPUT",
	"SELECT",
	"TEXTAREA"
];
function Co(e) {
	let t = e.composedPath?.()?.[0] || e.target;
	return t?.nodeType === 1 ? So.includes(t.nodeName) || t.hasAttribute("contenteditable") || !!t.closest(".nokey") : !1;
}
var wo = (e) => "clientX" in e, To = (e, t) => {
	let n = wo(e), r = n ? e.clientX : e.touches?.[0].clientX, i = n ? e.clientY : e.touches?.[0].clientY;
	return {
		x: r - (t?.left ?? 0),
		y: i - (t?.top ?? 0)
	};
}, Eo = (e, t, n, r, i) => {
	let a = t.querySelectorAll(`.${e}`);
	return !a || !a.length ? null : Array.from(a).map((t) => {
		let a = t.getBoundingClientRect();
		return {
			id: t.getAttribute("data-handleid"),
			type: e,
			nodeId: i,
			position: t.getAttribute("data-handlepos"),
			x: (a.left - n.left) / r,
			y: (a.top - n.top) / r,
			...bo(t)
		};
	});
};
function Do({ sourceX: e, sourceY: t, targetX: n, targetY: r, sourceControlX: i, sourceControlY: a, targetControlX: o, targetControlY: s }) {
	let c = e * .125 + i * .375 + o * .375 + n * .125, l = t * .125 + a * .375 + s * .375 + r * .125;
	return [
		c,
		l,
		Math.abs(c - e),
		Math.abs(l - t)
	];
}
function Oo(e, t) {
	return e >= 0 ? .5 * e : t * 25 * Math.sqrt(-e);
}
function ko({ pos: e, x1: t, y1: n, x2: r, y2: i, c: a }) {
	switch (e) {
		case V.Left: return [t - Oo(t - r, a), n];
		case V.Right: return [t + Oo(r - t, a), n];
		case V.Top: return [t, n - Oo(n - i, a)];
		case V.Bottom: return [t, n + Oo(i - n, a)];
	}
}
function Ao({ sourceX: e, sourceY: t, sourcePosition: n = V.Bottom, targetX: r, targetY: i, targetPosition: a = V.Top, curvature: o = .25 }) {
	let [s, c] = ko({
		pos: n,
		x1: e,
		y1: t,
		x2: r,
		y2: i,
		c: o
	}), [l, u] = ko({
		pos: a,
		x1: r,
		y1: i,
		x2: e,
		y2: t,
		c: o
	}), [d, f, p, m] = Do({
		sourceX: e,
		sourceY: t,
		targetX: r,
		targetY: i,
		sourceControlX: s,
		sourceControlY: c,
		targetControlX: l,
		targetControlY: u
	});
	return [
		`M${e},${t} C${s},${c} ${l},${u} ${r},${i}`,
		d,
		f,
		p,
		m
	];
}
function jo({ sourceX: e, sourceY: t, targetX: n, targetY: r }) {
	let i = Math.abs(n - e) / 2, a = n < e ? n + i : n - i, o = Math.abs(r - t) / 2;
	return [
		a,
		r < t ? r + o : r - o,
		i,
		o
	];
}
function Mo({ sourceNode: e, targetNode: t, selected: n = !1, zIndex: r = 0, elevateOnSelect: i = !1, zIndexMode: a = "basic" }) {
	return a === "manual" ? r : (i && n ? r + 1e3 : r) + Math.max(e.parentId || i && e.selected ? e.internals.z : 0, t.parentId || i && t.selected ? t.internals.z : 0);
}
function No({ sourceNode: e, targetNode: t, width: n, height: r, transform: i }) {
	let a = Ga(Ya(e), Ya(t));
	return a.x === a.x2 && (a.x2 += 1), a.y === a.y2 && (a.y2 += 1), Qa({
		x: -i[0] / i[2],
		y: -i[1] / i[2],
		width: n / i[2],
		height: r / i[2]
	}, qa(a)) > 0;
}
var Po = ({ source: e, sourceHandle: t, target: n, targetHandle: r }) => `xy-edge__${e}${t || ""}-${n}${r || ""}`, Fo = (e, t) => t.some((t) => t.source === e.source && t.target === e.target && (t.sourceHandle === e.sourceHandle || !t.sourceHandle && !e.sourceHandle) && (t.targetHandle === e.targetHandle || !t.targetHandle && !e.targetHandle)), Io = (e, t, n = {}) => {
	if (!e.source || !e.target) return n.onError?.("006", ya.error006()), t;
	let r = n.getEdgeId || Po, i;
	return i = Aa(e) ? { ...e } : {
		...e,
		id: r(e)
	}, Fo(i, t) ? t : (i.sourceHandle === null && delete i.sourceHandle, i.targetHandle === null && delete i.targetHandle, t.concat(i));
};
function Lo({ sourceX: e, sourceY: t, targetX: n, targetY: r }) {
	let [i, a, o, s] = jo({
		sourceX: e,
		sourceY: t,
		targetX: n,
		targetY: r
	});
	return [
		`M ${e},${t}L ${n},${r}`,
		i,
		a,
		o,
		s
	];
}
var Ro = {
	[V.Left]: {
		x: -1,
		y: 0
	},
	[V.Right]: {
		x: 1,
		y: 0
	},
	[V.Top]: {
		x: 0,
		y: -1
	},
	[V.Bottom]: {
		x: 0,
		y: 1
	}
}, zo = ({ source: e, sourcePosition: t = V.Bottom, target: n }) => t === V.Left || t === V.Right ? e.x < n.x ? {
	x: 1,
	y: 0
} : {
	x: -1,
	y: 0
} : e.y < n.y ? {
	x: 0,
	y: 1
} : {
	x: 0,
	y: -1
}, Bo = (e, t) => Math.sqrt((t.x - e.x) ** 2 + (t.y - e.y) ** 2), Vo = (e, t, n) => e.x === t.x && t.x === n.x && (t.y - e.y) * (n.y - t.y) >= 0 || e.y === t.y && t.y === n.y && (t.x - e.x) * (n.x - t.x) >= 0;
function Ho({ source: e, sourcePosition: t = V.Bottom, target: n, targetPosition: r = V.Top, center: i, offset: a, stepPosition: o }) {
	let s = Ro[t], c = Ro[r], l = {
		x: e.x + s.x * a,
		y: e.y + s.y * a
	}, u = {
		x: n.x + c.x * a,
		y: n.y + c.y * a
	}, d = zo({
		source: l,
		sourcePosition: t,
		target: u
	}), f = d.x === 0 ? "y" : "x", p = d[f], m = [], h, g, _ = {
		x: 0,
		y: 0
	}, v = {
		x: 0,
		y: 0
	}, [, , y, b] = jo({
		sourceX: e.x,
		sourceY: e.y,
		targetX: n.x,
		targetY: n.y
	});
	if (s[f] * c[f] === -1) {
		f === "x" ? (h = i.x ?? l.x + (u.x - l.x) * o, g = i.y ?? (l.y + u.y) / 2) : (h = i.x ?? (l.x + u.x) / 2, g = i.y ?? l.y + (u.y - l.y) * o);
		let e = [{
			x: h,
			y: l.y
		}, {
			x: h,
			y: u.y
		}], t = [{
			x: l.x,
			y: g
		}, {
			x: u.x,
			y: g
		}];
		m = s[f] === p ? f === "x" ? e : t : f === "x" ? t : e;
	} else {
		let i = [{
			x: l.x,
			y: u.y
		}], o = [{
			x: u.x,
			y: l.y
		}];
		if (m = f === "x" ? s.x === p ? o : i : s.y === p ? i : o, t === r) {
			let t = Math.abs(e[f] - n[f]);
			if (t <= a) {
				let r = Math.min(a - 1, a - t);
				s[f] === p ? _[f] = (l[f] > e[f] ? -1 : 1) * r : v[f] = (u[f] > n[f] ? -1 : 1) * r;
			}
		}
		if (t !== r) {
			let e = f === "x" ? "y" : "x", t = s[f] === c[e], n = l[e] > u[e], r = l[e] < u[e];
			(s[f] === 1 && (!t && n || t && r) || s[f] !== 1 && (!t && r || t && n)) && (m = f === "x" ? i : o);
		}
		let d = {
			x: l.x + _.x,
			y: l.y + _.y
		}, y = {
			x: u.x + v.x,
			y: u.y + v.y
		};
		Math.max(Math.abs(d.x - m[0].x), Math.abs(y.x - m[0].x)) >= Math.max(Math.abs(d.y - m[0].y), Math.abs(y.y - m[0].y)) ? (h = (d.x + y.x) / 2, g = m[0].y) : (h = m[0].x, g = (d.y + y.y) / 2);
	}
	let x = {
		x: l.x + _.x,
		y: l.y + _.y
	}, S = {
		x: u.x + v.x,
		y: u.y + v.y
	}, C = [];
	for (let t of [
		e,
		x,
		...m,
		S,
		n
	]) {
		let [e, n] = C.slice(-2);
		n && Vo(e, n, t) && C.pop(), C.push(t);
	}
	return [
		C,
		h,
		g,
		y,
		b
	];
}
function Uo(e, t, n, r) {
	let i = Math.min(Bo(e, t) / 2, Bo(t, n) / 2, r), { x: a, y: o } = t;
	if (e.x === a && a === n.x || e.y === o && o === n.y) return `L${a} ${o}`;
	if (e.y === o) {
		let t = e.x < n.x ? -1 : 1, r = e.y < n.y ? 1 : -1;
		return `L ${a + i * t},${o}Q ${a},${o} ${a},${o + i * r}`;
	}
	let s = e.x < n.x ? 1 : -1;
	return `L ${a},${o + i * (e.y < n.y ? -1 : 1)}Q ${a},${o} ${a + i * s},${o}`;
}
function Wo({ sourceX: e, sourceY: t, sourcePosition: n = V.Bottom, targetX: r, targetY: i, targetPosition: a = V.Top, borderRadius: o = 5, centerX: s, centerY: c, offset: l = 20, stepPosition: u = .5 }) {
	let [d, f, p, m, h] = Ho({
		source: {
			x: e,
			y: t
		},
		sourcePosition: n,
		target: {
			x: r,
			y: i
		},
		targetPosition: a,
		center: {
			x: s,
			y: c
		},
		offset: l,
		stepPosition: u
	}), g = `M${d[0].x} ${d[0].y}`;
	for (let e = 1; e < d.length - 1; e++) g += Uo(d[e - 1], d[e], d[e + 1], o);
	return g += `L${d[d.length - 1].x} ${d[d.length - 1].y}`, [
		g,
		f,
		p,
		m,
		h
	];
}
function Go(e) {
	return e && !!(e.internals.handleBounds || e.handles?.length) && !!(e.measured.width || e.width || e.initialWidth);
}
function Ko(e) {
	let { sourceNode: t, targetNode: n } = e;
	if (!Go(t) || !Go(n)) return null;
	let r = t.internals.handleBounds || qo(t.handles), i = n.internals.handleBounds || qo(n.handles), a = Yo(r?.source ?? [], e.sourceHandle), o = Yo(e.connectionMode === Ca.Strict ? i?.target ?? [] : (i?.target ?? []).concat(i?.source ?? []), e.targetHandle);
	if (!a || !o) return e.onError?.("008", ya.error008(a ? "target" : "source", {
		id: e.id,
		sourceHandle: e.sourceHandle,
		targetHandle: e.targetHandle
	})), null;
	let s = a?.position || V.Bottom, c = o?.position || V.Top, l = Jo(t, a, s), u = Jo(n, o, c);
	return {
		sourceX: l.x,
		sourceY: l.y,
		targetX: u.x,
		targetY: u.y,
		sourcePosition: s,
		targetPosition: c
	};
}
function qo(e) {
	if (!e) return null;
	let t = [], n = [];
	for (let r of e) r.width = r.width ?? 1, r.height = r.height ?? 1, r.type === "source" ? t.push(r) : r.type === "target" && n.push(r);
	return {
		source: t,
		target: n
	};
}
function Jo(e, t, n = V.Left, r = !1) {
	let i = (t?.x ?? 0) + e.internals.positionAbsolute.x, a = (t?.y ?? 0) + e.internals.positionAbsolute.y, { width: o, height: s } = t ?? fo(e);
	if (r) return {
		x: i + o / 2,
		y: a + s / 2
	};
	switch (t?.position ?? n) {
		case V.Top: return {
			x: i + o / 2,
			y: a
		};
		case V.Right: return {
			x: i + o,
			y: a + s / 2
		};
		case V.Bottom: return {
			x: i + o / 2,
			y: a + s
		};
		case V.Left: return {
			x: i,
			y: a + s / 2
		};
	}
}
function Yo(e, t) {
	return e && (t ? e.find((e) => e.id === t) : e[0]) || null;
}
function Xo(e, t) {
	return e ? typeof e == "string" ? e : `${t ? `${t}__` : ""}${Object.keys(e).sort().map((t) => `${t}=${e[t]}`).join("&")}` : "";
}
function Zo(e, { id: t, defaultColor: n, defaultMarkerStart: r, defaultMarkerEnd: i }) {
	let a = /* @__PURE__ */ new Set();
	return e.reduce((e, o) => ([o.markerStart || r, o.markerEnd || i].forEach((r) => {
		if (r && typeof r == "object") {
			let i = Xo(r, t);
			a.has(i) || (e.push({
				id: i,
				color: r.color || n,
				...r
			}), a.add(i));
		}
	}), e), []).sort((e, t) => e.id.localeCompare(t.id));
}
var Qo = 1e3, $o = 10, es = {
	nodeOrigin: [0, 0],
	nodeExtent: ba,
	elevateNodesOnSelect: !0,
	zIndexMode: "basic",
	defaults: {}
}, ts = {
	...es,
	checkEquality: !0
};
function ns(e, t) {
	let n = { ...e };
	for (let e in t) t[e] !== void 0 && (n[e] = t[e]);
	return n;
}
function rs(e, t, n) {
	let r = ns(es, n);
	for (let n of e.values()) if (n.parentId) cs(n, e, t, r);
	else {
		let e = H(Na(n, r.nodeOrigin), uo(n.extent) ? n.extent : r.nodeExtent, fo(n));
		n.internals.positionAbsolute = e;
	}
}
function is(e, t) {
	if (!e.handles) return e.measured ? t?.internals.handleBounds : void 0;
	let n = [], r = [];
	for (let t of e.handles) {
		let i = {
			id: t.id,
			width: t.width ?? 1,
			height: t.height ?? 1,
			nodeId: e.id,
			x: t.x,
			y: t.y,
			position: t.position,
			type: t.type
		};
		t.type === "source" ? n.push(i) : t.type === "target" && r.push(i);
	}
	return {
		source: n,
		target: r
	};
}
function as(e) {
	return e === "manual";
}
function os(e, t, n, r = {}) {
	let i = ns(ts, r), a = { i: 0 }, o = new Map(t), s = i?.elevateNodesOnSelect && !as(i.zIndexMode) ? Qo : 0, c = e.length > 0, l = !1;
	t.clear(), n.clear();
	for (let u of e) {
		let e = o.get(u.id);
		if (i.checkEquality && u === e?.internals.userNode) t.set(u.id, e);
		else {
			let n = H(Na(u, i.nodeOrigin), uo(u.extent) ? u.extent : i.nodeExtent, fo(u));
			e = {
				...i.defaults,
				...u,
				measured: {
					width: u.measured?.width,
					height: u.measured?.height
				},
				internals: {
					positionAbsolute: n,
					handleBounds: is(u, e),
					z: ls(u, s, i.zIndexMode),
					userNode: u
				}
			}, t.set(u.id, e);
		}
		e.measured !== void 0 && e.measured.width !== void 0 && e.measured.height !== void 0 || e.hidden || (c = !1), u.parentId && cs(e, t, n, r, a), l ||= u.selected ?? !1;
	}
	return {
		nodesInitialized: c,
		hasSelectedNodes: l
	};
}
function ss(e, t) {
	if (!e.parentId) return;
	let n = t.get(e.parentId);
	n ? n.set(e.id, e) : t.set(e.parentId, /* @__PURE__ */ new Map([[e.id, e]]));
}
function cs(e, t, n, r, i) {
	let { elevateNodesOnSelect: a, nodeOrigin: o, nodeExtent: s, zIndexMode: c } = ns(es, r), l = e.parentId, u = t.get(l);
	if (!u) {
		console.warn(`Parent node ${l} not found. Please make sure that parent nodes are in front of their child nodes in the nodes array.`);
		return;
	}
	ss(e, n), i && !u.parentId && u.internals.rootParentIndex === void 0 && c === "auto" && (u.internals.rootParentIndex = ++i.i, u.internals.z = u.internals.z + i.i * $o), i && u.internals.rootParentIndex !== void 0 && (i.i = u.internals.rootParentIndex);
	let { x: d, y: f, z: p } = us(e, u, o, s, a && !as(c) ? Qo : 0, c), { positionAbsolute: m } = e.internals, h = d !== m.x || f !== m.y;
	(h || p !== e.internals.z) && t.set(e.id, {
		...e,
		internals: {
			...e.internals,
			positionAbsolute: h ? {
				x: d,
				y: f
			} : m,
			z: p
		}
	});
}
function ls(e, t, n) {
	let r = eo(e.zIndex) ? e.zIndex : 0;
	return as(n) ? r : r + (e.selected ? t : 0);
}
function us(e, t, n, r, i, a) {
	let { x: o, y: s } = t.internals.positionAbsolute, c = fo(e), l = Na(e, n), u = uo(e.extent) ? H(l, e.extent, c) : l, d = H({
		x: o + u.x,
		y: s + u.y
	}, r, c);
	e.extent === "parent" && (d = U(d, c, t));
	let f = ls(e, i, a), p = t.internals.z ?? 0;
	return {
		x: d.x,
		y: d.y,
		z: p >= f ? p + 1 : f
	};
}
function ds(e, t, n, r = [0, 0]) {
	let i = [], a = /* @__PURE__ */ new Map();
	for (let n of e) {
		let e = t.get(n.parentId);
		if (!e) continue;
		let r = Xa(a.get(n.parentId)?.expandedRect ?? Ja(e), n.rect);
		a.set(n.parentId, {
			expandedRect: r,
			parent: e
		});
	}
	return a.size > 0 && a.forEach(({ expandedRect: t, parent: a }, o) => {
		let s = a.internals.positionAbsolute, c = fo(a), l = a.origin ?? r, u = t.x < s.x ? Math.round(Math.abs(s.x - t.x)) : 0, d = t.y < s.y ? Math.round(Math.abs(s.y - t.y)) : 0, f = Math.max(c.width, Math.round(t.width)), p = Math.max(c.height, Math.round(t.height)), m = (f - c.width) * l[0], h = (p - c.height) * l[1];
		(u > 0 || d > 0 || m || h) && (i.push({
			id: o,
			type: "position",
			position: {
				x: a.position.x - u + m,
				y: a.position.y - d + h
			}
		}), n.get(o)?.forEach((t) => {
			e.some((e) => e.id === t.id) || i.push({
				id: t.id,
				type: "position",
				position: {
					x: t.position.x + u,
					y: t.position.y + d
				}
			});
		})), (c.width < t.width || c.height < t.height || u || d) && i.push({
			id: o,
			type: "dimensions",
			setAttributes: !0,
			dimensions: {
				width: f + (u ? l[0] * u - m : 0),
				height: p + (d ? l[1] * d - h : 0)
			}
		});
	}), i;
}
function fs(e, t, n, r, i, a, o) {
	let s = r?.querySelector(".xyflow__viewport"), c = !1;
	if (!s) return {
		changes: [],
		updatedInternals: c
	};
	let l = [], u = window.getComputedStyle(s), { m22: d } = new window.DOMMatrixReadOnly(u.transform), f = [];
	for (let r of e.values()) {
		let e = t.get(r.id);
		if (!e) continue;
		if (e.hidden) {
			t.set(e.id, {
				...e,
				internals: {
					...e.internals,
					handleBounds: void 0
				}
			}), c = !0;
			continue;
		}
		let s = bo(r.nodeElement), u = e.measured.width !== s.width || e.measured.height !== s.height;
		if (s.width && s.height && (u || !e.internals.handleBounds || r.force)) {
			let p = r.nodeElement.getBoundingClientRect(), m = uo(e.extent) ? e.extent : a, { positionAbsolute: h } = e.internals;
			if (e.parentId && e.extent === "parent") {
				let n = t.get(e.parentId);
				n && (h = U(h, s, n));
			} else m && (h = H(h, m, s));
			let g = {
				...e,
				measured: s,
				internals: {
					...e.internals,
					positionAbsolute: h,
					handleBounds: {
						source: Eo("source", r.nodeElement, p, d, e.id),
						target: Eo("target", r.nodeElement, p, d, e.id)
					}
				}
			};
			t.set(e.id, g), e.parentId && cs(g, t, n, {
				nodeOrigin: i,
				zIndexMode: o
			}), c = !0, u && (l.push({
				id: e.id,
				type: "dimensions",
				dimensions: s
			}), e.expandParent && e.parentId && f.push({
				id: e.id,
				parentId: e.parentId,
				rect: Ja(g, i)
			}));
		}
	}
	if (f.length > 0) {
		let e = ds(f, t, n, i);
		l.push(...e);
	}
	return {
		changes: l,
		updatedInternals: c
	};
}
async function ps({ delta: e, panZoom: t, transform: n, translateExtent: r, width: i, height: a }) {
	if (!t || !e.x && !e.y) return !1;
	let o = await t.setViewportConstrained({
		x: n[0] + e.x,
		y: n[1] + e.y,
		zoom: n[2]
	}, [[0, 0], [i, a]], r);
	return !!o && (o.x !== n[0] || o.y !== n[1] || o.k !== n[2]);
}
function ms(e, t, n, r, i, a) {
	let o = i, s = r.get(o) || /* @__PURE__ */ new Map();
	r.set(o, s.set(n, t)), o = `${i}-${e}`;
	let c = r.get(o) || /* @__PURE__ */ new Map();
	if (r.set(o, c.set(n, t)), a) {
		o = `${i}-${e}-${a}`;
		let s = r.get(o) || /* @__PURE__ */ new Map();
		r.set(o, s.set(n, t));
	}
}
function hs(e, t, n) {
	e.clear(), t.clear();
	for (let r of n) {
		let { source: n, target: i, sourceHandle: a = null, targetHandle: o = null } = r, s = {
			edgeId: r.id,
			source: n,
			target: i,
			sourceHandle: a,
			targetHandle: o
		}, c = `${n}-${a}--${i}-${o}`;
		ms("source", s, `${i}-${o}--${n}-${a}`, e, n, a), ms("target", s, c, e, i, o), t.set(r.id, r);
	}
}
function gs(e, t) {
	if (!e.parentId) return !1;
	let n = t.get(e.parentId);
	return n ? n.selected ? !0 : gs(n, t) : !1;
}
function _s(e, t, n) {
	let r = e;
	do {
		if (r?.matches?.(t)) return !0;
		if (r === n) return !1;
		r = r?.parentElement;
	} while (r);
	return !1;
}
function vs(e, t, n, r, i) {
	let a = /* @__PURE__ */ new Map();
	for (let [o, s] of e) if ((i ? s.id === r : s.selected || s.id === r) && (i || !s.parentId || !gs(s, e)) && (s.draggable || t && s.draggable === void 0)) {
		let t = e.get(o);
		t && a.set(o, {
			id: o,
			position: t.position || {
				x: 0,
				y: 0
			},
			distance: {
				x: n.x - t.internals.positionAbsolute.x,
				y: n.y - t.internals.positionAbsolute.y
			},
			extent: t.extent,
			parentId: t.parentId,
			origin: t.origin,
			expandParent: t.expandParent,
			internals: { positionAbsolute: t.internals.positionAbsolute || {
				x: 0,
				y: 0
			} },
			measured: {
				width: t.measured.width ?? 0,
				height: t.measured.height ?? 0
			}
		});
	}
	return a;
}
function ys({ nodeId: e, dragItems: t, nodeLookup: n, dragging: r = !0 }) {
	let i = [];
	for (let [e, a] of t) {
		let t = n.get(e)?.internals.userNode;
		t && i.push({
			...t,
			position: a.position,
			dragging: r
		});
	}
	if (!e) return [i[0], i];
	let a = n.get(e)?.internals.userNode;
	return [a ? {
		...a,
		position: t.get(e)?.position || a.position,
		dragging: r
	} : i[0], i];
}
function bs({ dragItems: e, snapGrid: t, x: n, y: r }) {
	let i = e.values().next().value;
	if (!i) return null;
	let a = {
		x: n - i.distance.x,
		y: r - i.distance.y
	}, o = no(a, t);
	return {
		x: o.x - a.x,
		y: o.y - a.y
	};
}
function xs({ onNodeMouseDown: e, getStoreItems: t, onDragStart: n, onDrag: r, onDragStop: i }) {
	let a = {
		x: null,
		y: null
	}, o = 0, s = /* @__PURE__ */ new Map(), c = !1, l = {
		x: 0,
		y: 0
	}, u = null, d = !1, f = null, p = !1, m = !1, h = null;
	function g({ noDragClassName: g, handleSelector: _, domNode: v, isSelectable: y, nodeId: b, nodeClickDistance: x = 0 }) {
		f = Vt(v);
		function S({ x: e, y: n }) {
			let { nodeLookup: i, nodeExtent: o, snapGrid: c, snapToGrid: l, nodeOrigin: u, onNodeDrag: d, onSelectionDrag: f, onError: p, updateNodePositions: g } = t();
			a = {
				x: e,
				y: n
			};
			let _ = !1, v = s.size > 1, y = v && o ? Ka(Fa(s)) : null, x = v && l ? bs({
				dragItems: s,
				snapGrid: c,
				x: e,
				y: n
			}) : null;
			for (let [t, r] of s) {
				if (!i.has(t)) continue;
				let a = {
					x: e - r.distance.x,
					y: n - r.distance.y
				};
				l && (a = x ? {
					x: Math.round(a.x + x.x),
					y: Math.round(a.y + x.y)
				} : no(a, c));
				let s = null;
				if (v && o && !r.extent && y) {
					let { positionAbsolute: e } = r.internals, t = e.x - y.x + o[0][0], n = e.x + r.measured.width - y.x2 + o[1][0], i = e.y - y.y + o[0][1], a = e.y + r.measured.height - y.y2 + o[1][1];
					s = [[t, i], [n, a]];
				}
				let { position: d, positionAbsolute: f } = Ba({
					nodeId: t,
					nextPosition: a,
					nodeLookup: i,
					nodeExtent: s || o,
					nodeOrigin: u,
					onError: p
				});
				_ = _ || r.position.x !== d.x || r.position.y !== d.y, r.position = d, r.internals.positionAbsolute = f;
			}
			if (m ||= _, _ && (g(s, !0), h && (r || d || !b && f))) {
				let [e, t] = ys({
					nodeId: b,
					dragItems: s,
					nodeLookup: i
				});
				r?.(h, s, e, t), d?.(h, e, t), b || f?.(h, t);
			}
		}
		async function C() {
			if (!u) return;
			let { transform: e, panBy: n, autoPanSpeed: r, autoPanOnNodeDrag: i } = t();
			if (!i) {
				c = !1, cancelAnimationFrame(o);
				return;
			}
			let [s, d] = Wa(l, u, r);
			(s !== 0 || d !== 0) && (a.x = (a.x ?? 0) - s / e[2], a.y = (a.y ?? 0) - d / e[2], await n({
				x: s,
				y: d
			}) && S(a)), o = requestAnimationFrame(C);
		}
		function w(r) {
			let { nodeLookup: i, multiSelectionActive: o, nodesDraggable: c, transform: l, snapGrid: f, snapToGrid: p, selectNodesOnDrag: m, onNodeDragStart: h, onSelectionDragStart: g, unselectNodesAndEdges: _ } = t();
			d = !0;
			let v = !!b && !o && !i.get(b)?.selected;
			m && y || o || !b || i.get(b)?.selected || _(), y && m && b && e?.(b);
			let x = yo(r.sourceEvent, {
				transform: l,
				snapGrid: f,
				snapToGrid: p,
				containerBounds: u
			});
			if (a = x, s = vs(i, c, x, b, v), s.size > 0 && (n || h || !b && g)) {
				let [e, t] = ys({
					nodeId: b,
					dragItems: s,
					nodeLookup: i
				});
				n?.(r.sourceEvent, s, e, t), h?.(r.sourceEvent, e, t), b || g?.(r.sourceEvent, t);
			}
		}
		let T = nn().clickDistance(x).on("start", (e) => {
			let { domNode: n, nodeDragThreshold: r, transform: i, snapGrid: o, snapToGrid: s } = t();
			u = n?.getBoundingClientRect() || null, p = !1, m = !1, h = e.sourceEvent, r === 0 && w(e), a = yo(e.sourceEvent, {
				transform: i,
				snapGrid: o,
				snapToGrid: s,
				containerBounds: u
			}), l = To(e.sourceEvent, u);
		}).on("drag", (e) => {
			let { autoPanOnNodeDrag: n, transform: r, snapGrid: i, snapToGrid: o, nodeDragThreshold: f, nodeLookup: m } = t(), g = yo(e.sourceEvent, {
				transform: r,
				snapGrid: i,
				snapToGrid: o,
				containerBounds: u
			});
			if (h = e.sourceEvent, (e.sourceEvent.type === "touchmove" && e.sourceEvent.touches.length > 1 || b && !m.has(b)) && (p = !0), !p) {
				if (!c && n && d && (c = !0, C()), !d) {
					let t = To(e.sourceEvent, u), n = t.x - l.x, r = t.y - l.y;
					Math.sqrt(n * n + r * r) > f && w(e);
				}
				(a.x !== g.xSnapped || a.y !== g.ySnapped) && s && d && (l = To(e.sourceEvent, u), S(g));
			}
		}).on("end", (e) => {
			if (!d || p) p && s.size > 0 && t().updateNodePositions(s, !1);
			else if (c = !1, d = !1, cancelAnimationFrame(o), s.size > 0) {
				let { nodeLookup: n, updateNodePositions: r, onNodeDragStop: a, onSelectionDragStop: o } = t();
				if (m &&= (r(s, !1), !1), i || a || !b && o) {
					let [t, r] = ys({
						nodeId: b,
						dragItems: s,
						nodeLookup: n,
						dragging: !1
					});
					i?.(e.sourceEvent, s, t, r), a?.(e.sourceEvent, t, r), b || o?.(e.sourceEvent, r);
				}
			}
		}).filter((e) => {
			let t = e.target;
			return !e.button && (!g || !_s(t, `.${g}`, v)) && (!_ || _s(t, _, v));
		});
		f.call(T);
	}
	function _() {
		f?.on(".drag", null);
	}
	return {
		update: g,
		destroy: _
	};
}
function Ss(e, t, n) {
	let r = [], i = {
		x: e.x - n,
		y: e.y - n,
		width: n * 2,
		height: n * 2
	};
	for (let e of t.values()) Qa(i, Ja(e)) > 0 && r.push(e);
	return r;
}
var Cs = 250;
function ws(e, t, n, r) {
	let i = [], a = Infinity, o = Ss(e, n, t + Cs);
	for (let n of o) {
		let o = [...n.internals.handleBounds?.source ?? [], ...n.internals.handleBounds?.target ?? []];
		for (let s of o) {
			if (r.nodeId === s.nodeId && r.type === s.type && r.id === s.id) continue;
			let { x: o, y: c } = Jo(n, s, s.position, !0), l = Math.sqrt((o - e.x) ** 2 + (c - e.y) ** 2);
			l > t || (l < a ? (i = [{
				...s,
				x: o,
				y: c
			}], a = l) : l === a && i.push({
				...s,
				x: o,
				y: c
			}));
		}
	}
	if (!i.length) return null;
	if (i.length > 1) {
		let e = r.type === "source" ? "target" : "source";
		return i.find((t) => t.type === e) ?? i[0];
	}
	return i[0];
}
function Ts(e, t, n, r, i, a = !1) {
	let o = r.get(e);
	if (!o) return null;
	let s = i === "strict" ? o.internals.handleBounds?.[t] : [...o.internals.handleBounds?.source ?? [], ...o.internals.handleBounds?.target ?? []], c = (n ? s?.find((e) => e.id === n) : s?.[0]) ?? null;
	return c && a ? {
		...c,
		...Jo(o, c, c.position, !0)
	} : c;
}
function Es(e, t) {
	return e || (t?.classList.contains("target") ? "target" : t?.classList.contains("source") ? "source" : null);
}
function Ds(e, t) {
	let n = null;
	return t ? n = !0 : e && !t && (n = !1), n;
}
var Os = () => !0;
function ks(e, { connectionMode: t, connectionRadius: n, handleId: r, nodeId: i, edgeUpdaterType: a, isTarget: o, domNode: s, nodeLookup: c, lib: l, autoPanOnConnect: u, flowId: d, panBy: f, cancelConnection: p, onConnectStart: m, onConnect: h, onConnectEnd: g, isValidConnection: _ = Os, onReconnectEnd: v, updateConnection: y, getTransform: b, getFromHandle: x, autoPanSpeed: S, dragThreshold: C = 1, handleDomNode: w }) {
	let T = xo(e.target), E = 0, D, { x: O, y: k } = To(e), A = Es(a, w), j = s?.getBoundingClientRect(), M = !1;
	if (!j || !A) return;
	let ee = Ts(i, A, r, c, t);
	if (!ee) return;
	let N = To(e, j), te = !1, P = null, F = !1, ne = null;
	function re() {
		if (!u || !j) return;
		let [e, t] = Wa(N, j, S);
		f({
			x: e,
			y: t
		}), E = requestAnimationFrame(re);
	}
	let I = {
		...ee,
		nodeId: i,
		type: A,
		position: ee.position
	}, L = c.get(i), R = {
		inProgress: !0,
		isValid: null,
		from: Jo(L, I, V.Left, !0),
		fromHandle: I,
		fromPosition: I.position,
		fromNode: L,
		to: N,
		toHandle: null,
		toPosition: ka[I.position],
		toNode: null,
		pointer: N
	};
	function z() {
		M = !0, y(R), m?.(e, {
			nodeId: i,
			handleId: r,
			handleType: A
		});
	}
	C === 0 && z();
	function ie(e) {
		if (!M) {
			let { x: t, y: n } = To(e), r = t - O, i = n - k;
			if (!(r * r + i * i > C * C)) return;
			z();
		}
		if (!x() || !I) {
			ae(e);
			return;
		}
		let a = b();
		N = To(e, j), D = ws(ro(N, a, !1, [1, 1]), n, c, I), te ||= (re(), !0);
		let s = As(e, {
			handle: D,
			connectionMode: t,
			fromNodeId: i,
			fromHandleId: r,
			fromType: o ? "target" : "source",
			isValidConnection: _,
			doc: T,
			lib: l,
			flowId: d,
			nodeLookup: c
		});
		ne = s.handleDomNode, P = s.connection, F = Ds(!!D, s.isValid);
		let u = c.get(i), f = u ? Jo(u, I, V.Left, !0) : R.from, p = {
			...R,
			from: f,
			isValid: F,
			to: s.toHandle && F ? io({
				x: s.toHandle.x,
				y: s.toHandle.y
			}, a) : N,
			toHandle: s.toHandle,
			toPosition: F && s.toHandle ? s.toHandle.position : ka[I.position],
			toNode: s.toHandle ? c.get(s.toHandle.nodeId) : null,
			pointer: N
		};
		y(p), R = p;
	}
	function ae(e) {
		if (!("touches" in e && e.touches.length > 0)) {
			if (M) {
				(D || ne) && P && F && h?.(P);
				let { inProgress: t, ...n } = R, r = {
					...n,
					toPosition: R.toHandle ? R.toPosition : null
				};
				g?.(e, r), a && v?.(e, r);
			}
			p(), cancelAnimationFrame(E), te = !1, F = !1, P = null, ne = null, T.removeEventListener("mousemove", ie), T.removeEventListener("mouseup", ae), T.removeEventListener("touchmove", ie), T.removeEventListener("touchend", ae);
		}
	}
	T.addEventListener("mousemove", ie), T.addEventListener("mouseup", ae), T.addEventListener("touchmove", ie), T.addEventListener("touchend", ae);
}
function As(e, { handle: t, connectionMode: n, fromNodeId: r, fromHandleId: i, fromType: a, doc: o, lib: s, flowId: c, isValidConnection: l = Os, nodeLookup: u }) {
	let d = a === "target", f = t ? o.querySelector(`.${s}-flow__handle[data-id="${c}-${t?.nodeId}-${t?.id}-${t?.type}"]`) : null, { x: p, y: m } = To(e), h = o.elementFromPoint(p, m), g = h?.classList.contains(`${s}-flow__handle`) ? h : f, _ = {
		handleDomNode: g,
		isValid: !1,
		connection: null,
		toHandle: null
	};
	if (g) {
		let e = Es(void 0, g), t = g.getAttribute("data-nodeid"), a = g.getAttribute("data-handleid"), o = g.classList.contains("connectable"), s = g.classList.contains("connectableend");
		if (!t || !e) return _;
		let c = {
			source: d ? t : r,
			sourceHandle: d ? a : i,
			target: d ? r : t,
			targetHandle: d ? i : a
		};
		_.connection = c, _.isValid = o && s && (n === Ca.Strict ? d && e === "source" || !d && e === "target" : t !== r || a !== i) && l(c), _.toHandle = Ts(t, e, a, u, n, !0);
	}
	return _;
}
var js = {
	onPointerDown: ks,
	isValid: As
};
function Ms({ domNode: e, panZoom: t, getTransform: n, getViewScale: r }) {
	let i = Vt(e);
	function a({ translateExtent: e, width: a, height: o, zoomStep: s = 1, pannable: c = !0, zoomable: l = !0, inversePan: u = !1 }) {
		let d = (e) => {
			if (e.sourceEvent.type !== "wheel" || !t) return;
			let r = n(), i = e.sourceEvent.ctrlKey && lo() ? 10 : 1, a = -e.sourceEvent.deltaY * (e.sourceEvent.deltaMode === 1 ? .05 : e.sourceEvent.deltaMode ? 1 : .002) * s, o = r[2] * 2 ** (a * i);
			t.scaleTo(o);
		}, f = [0, 0], p = va().on("start", (e) => {
			(e.sourceEvent.type === "mousedown" || e.sourceEvent.type === "touchstart") && (f = [e.sourceEvent.clientX ?? e.sourceEvent.touches[0].clientX, e.sourceEvent.clientY ?? e.sourceEvent.touches[0].clientY]);
		}).on("zoom", c ? (i) => {
			let s = n();
			if (i.sourceEvent.type !== "mousemove" && i.sourceEvent.type !== "touchmove" || !t) return;
			let c = [i.sourceEvent.clientX ?? i.sourceEvent.touches[0].clientX, i.sourceEvent.clientY ?? i.sourceEvent.touches[0].clientY], l = [c[0] - f[0], c[1] - f[1]];
			f = c;
			let d = r() * Math.max(s[2], Math.log(s[2])) * (u ? -1 : 1), p = {
				x: s[0] - l[0] * d,
				y: s[1] - l[1] * d
			}, m = [[0, 0], [a, o]];
			t.setViewportConstrained({
				x: p.x,
				y: p.y,
				zoom: s[2]
			}, m, e);
		} : null).on("zoom.wheel", l ? d : null);
		i.call(p, {});
	}
	function o() {
		i.on("zoom", null);
	}
	return {
		update: a,
		destroy: o,
		pointer: Ut
	};
}
var Ns = (e) => ({
	x: e.x,
	y: e.y,
	zoom: e.k
}), Ps = ({ x: e, y: t, zoom: n }) => ca.translate(e, t).scale(n), Fs = (e, t) => e.target.closest(`.${t}`), Is = (e, t) => t === 2 && Array.isArray(e) && e.includes(2), Ls = (e) => ((e *= 2) <= 1 ? e * e * e : (e -= 2) * e * e + 2) / 2, Rs = (e, t = 0, n = Ls, r = () => {}) => {
	let i = typeof t == "number" && t > 0;
	return i || r(), i ? e.transition().duration(t).ease(n).on("end", r) : e;
}, zs = (e) => {
	let t = e.ctrlKey && lo() ? 10 : 1;
	return -e.deltaY * (e.deltaMode === 1 ? .05 : e.deltaMode ? 1 : .002) * t;
};
function Bs({ zoomPanValues: e, noWheelClassName: t, d3Selection: n, d3Zoom: r, panOnScrollMode: i, panOnScrollSpeed: a, zoomOnPinch: o, onPanZoomStart: s, onPanZoom: c, onPanZoomEnd: l }) {
	return (u) => {
		if (Fs(u, t)) return u.ctrlKey && u.preventDefault(), !1;
		u.preventDefault(), u.stopImmediatePropagation();
		let d = n.property("__zoom").k || 1;
		if (u.ctrlKey && o) {
			let e = Ut(u), t = d * 2 ** zs(u);
			r.scaleTo(n, t, e, u);
			return;
		}
		let f = u.deltaMode === 1 ? 20 : 1, p = i === wa.Vertical ? 0 : u.deltaX * f, m = i === wa.Horizontal ? 0 : u.deltaY * f;
		!lo() && u.shiftKey && i !== wa.Vertical && (p = u.deltaY * f, m = 0), r.translateBy(n, -(p / d) * a, -(m / d) * a, { internal: !0 });
		let h = Ns(n.property("__zoom"));
		clearTimeout(e.panScrollTimeout), e.isPanScrolling ? c?.(u, h) : (e.isPanScrolling = !0, s?.(u, h)), e.panScrollTimeout = setTimeout(() => {
			l?.(u, h), e.isPanScrolling = !1;
		}, 150);
	};
}
function Vs({ noWheelClassName: e, preventScrolling: t, d3ZoomHandler: n }) {
	return function(r, i) {
		let a = r.type === "wheel", o = !t && a && !r.ctrlKey, s = Fs(r, e);
		if (r.ctrlKey && a && s && r.preventDefault(), o || s) return null;
		r.preventDefault(), n.call(this, r, i);
	};
}
function Hs({ zoomPanValues: e, onDraggingChange: t, onPanZoomStart: n }) {
	return (r) => {
		if (r.sourceEvent?.internal) return;
		let i = Ns(r.transform);
		e.mouseButton = r.sourceEvent?.button || 0, e.isZoomingOrPanning = !0, e.prevViewport = i, r.sourceEvent?.type === "mousedown" && t(!0), n && n?.(r.sourceEvent, i);
	};
}
function Us({ zoomPanValues: e, panOnDrag: t, onPaneContextMenu: n, onTransformChange: r, onPanZoom: i }) {
	return (a) => {
		e.usedRightMouseButton = !!(n && Is(t, e.mouseButton ?? 0)), a.sourceEvent?.sync || r([
			a.transform.x,
			a.transform.y,
			a.transform.k
		]), i && !a.sourceEvent?.internal && i?.(a.sourceEvent, Ns(a.transform));
	};
}
function Ws({ zoomPanValues: e, panOnDrag: t, panOnScroll: n, onDraggingChange: r, onPanZoomEnd: i, onPaneContextMenu: a }) {
	return (o) => {
		if (!o.sourceEvent?.internal && (e.isZoomingOrPanning = !1, a && Is(t, e.mouseButton ?? 0) && !e.usedRightMouseButton && o.sourceEvent && a(o.sourceEvent), e.usedRightMouseButton = !1, r(!1), i)) {
			let t = Ns(o.transform);
			e.prevViewport = t, clearTimeout(e.timerId), e.timerId = setTimeout(() => {
				i?.(o.sourceEvent, t);
			}, n ? 150 : 0);
		}
	};
}
function Gs({ panActivationKeyPressed: e, zoomActivationKeyPressed: t, zoomOnScroll: n, zoomOnPinch: r, panOnDrag: i, panOnScroll: a, zoomOnDoubleClick: o, userSelectionActive: s, noWheelClassName: c, noPanClassName: l, lib: u, connectionInProgress: d }) {
	return (f) => {
		let p = t || n, m = r && f.ctrlKey, h = f.type === "wheel";
		if (f.button === 1 && f.type === "mousedown" && (Fs(f, `${u}-flow__node`) || Fs(f, `${u}-flow__edge`) || Fs(f, `${u}-flow__selection`) || Fs(f, `${u}-flow__nodesselection`))) return !0;
		if (!i && !p && !a && !o && !r || s || d && !h || Fs(f, c) && h || Fs(f, l) && (!h || a && h && !t) || !r && f.ctrlKey && h) return !1;
		if (!r && f.type === "touchstart" && f.touches?.length > 1) return f.preventDefault(), !1;
		if (!p && !a && !m && h || !i && (f.type === "mousedown" || f.type === "touchstart") || Array.isArray(i) && !i.includes(f.button) && f.type === "mousedown") return !1;
		let g = Array.isArray(i) && i.includes(f.button) || !f.button || f.button <= 1;
		return (!f.ctrlKey || h || e) && g;
	};
}
function Ks({ domNode: e, minZoom: t, maxZoom: n, translateExtent: r, viewport: i, onPanZoom: a, onPanZoomStart: o, onPanZoomEnd: s, onDraggingChange: c }) {
	let l = {
		isZoomingOrPanning: !1,
		usedRightMouseButton: !1,
		prevViewport: {},
		mouseButton: 0,
		timerId: void 0,
		panScrollTimeout: void 0,
		isPanScrolling: !1
	}, u = e.getBoundingClientRect(), d = [[0, 0], [u.width, u.height]], f = typeof ResizeObserver < "u" ? new ResizeObserver((e) => {
		let t = e[0];
		t?.contentRect && (d = [[0, 0], [t.contentRect.width, t.contentRect.height]]);
	}) : null;
	f?.observe(e);
	let p = va().extent(() => d).scaleExtent([t, n]).translateExtent(r), m = Vt(e).call(p);
	b({
		x: i.x,
		y: i.y,
		zoom: Ha(i.zoom, t, n)
	}, [[0, 0], [u.width, u.height]], r);
	let h = m.on("wheel.zoom"), g = m.on("dblclick.zoom");
	p.wheelDelta(zs);
	async function _(e, t) {
		return m ? new Promise((n) => {
			p?.interpolate(t?.interpolate === "linear" ? or : br).transform(Rs(m, t?.duration, t?.ease, () => n(!0)), e);
		}) : !1;
	}
	function v({ noWheelClassName: e, noPanClassName: t, onPaneContextMenu: n, userSelectionActive: r, panOnScroll: i, panOnDrag: u, panOnScrollMode: d, panOnScrollSpeed: f, preventScrolling: _, zoomOnPinch: v, zoomOnScroll: y, zoomOnDoubleClick: b, panActivationKeyPressed: x = !1, zoomActivationKeyPressed: S, lib: C, onTransformChange: w, connectionInProgress: T, paneClickDistance: E, selectionOnDrag: D }) {
		r && !l.isZoomingOrPanning && p.on("zoom", null);
		let O = i && !S && !r;
		p.clickDistance(D ? Infinity : !eo(E) || E < 0 ? 0 : E);
		let k = O ? Bs({
			zoomPanValues: l,
			noWheelClassName: e,
			d3Selection: m,
			d3Zoom: p,
			panOnScrollMode: d,
			panOnScrollSpeed: f,
			zoomOnPinch: v,
			onPanZoomStart: o,
			onPanZoom: a,
			onPanZoomEnd: s
		}) : Vs({
			noWheelClassName: e,
			preventScrolling: _,
			d3ZoomHandler: h
		});
		m.on("wheel.zoom", k, { passive: !1 });
		let A = Hs({
			zoomPanValues: l,
			onDraggingChange: c,
			onPanZoomStart: o
		});
		p.on("start", A);
		let j = Us({
			zoomPanValues: l,
			panOnDrag: u,
			onPaneContextMenu: !!n,
			onPanZoom: a,
			onTransformChange: w
		});
		p.on("zoom", j);
		let M = Ws({
			zoomPanValues: l,
			panOnDrag: u,
			panOnScroll: i,
			onPaneContextMenu: n,
			onPanZoomEnd: s,
			onDraggingChange: c
		});
		p.on("end", M);
		let ee = Gs({
			panActivationKeyPressed: x,
			zoomActivationKeyPressed: S,
			panOnDrag: u,
			zoomOnScroll: y,
			panOnScroll: i,
			zoomOnDoubleClick: b,
			zoomOnPinch: v,
			userSelectionActive: r,
			noPanClassName: t,
			noWheelClassName: e,
			lib: C,
			connectionInProgress: T
		});
		p.filter(ee), b ? m.on("dblclick.zoom", g) : m.on("dblclick.zoom", null);
	}
	function y() {
		p.on("zoom", null), f?.disconnect();
	}
	async function b(e, t, n) {
		let r = Ps(e), i = p?.constrain()(r, t, n);
		return i && await _(i), i;
	}
	async function x(e, t) {
		let n = Ps(e);
		return await _(n, t), n;
	}
	function S(e) {
		if (m) {
			let t = Ps(e), n = m.property("__zoom");
			(n.k !== e.zoom || n.x !== e.x || n.y !== e.y) && p?.transform(m, t, null, { sync: !0 });
		}
	}
	function C() {
		let e = m ? la(m.node()) : {
			x: 0,
			y: 0,
			k: 1
		};
		return {
			x: e.x,
			y: e.y,
			zoom: e.k
		};
	}
	async function w(e, t) {
		return m ? new Promise((n) => {
			p?.interpolate(t?.interpolate === "linear" ? or : br).scaleTo(Rs(m, t?.duration, t?.ease, () => n(!0)), e);
		}) : !1;
	}
	async function T(e, t) {
		return m ? new Promise((n) => {
			p?.interpolate(t?.interpolate === "linear" ? or : br).scaleBy(Rs(m, t?.duration, t?.ease, () => n(!0)), e);
		}) : !1;
	}
	function E(e) {
		p?.scaleExtent(e);
	}
	function D(e) {
		p?.translateExtent(e);
	}
	function O(e) {
		let t = !eo(e) || e < 0 ? 0 : e;
		p?.clickDistance(t);
	}
	return {
		update: v,
		destroy: y,
		setViewport: x,
		setViewportConstrained: b,
		getViewport: C,
		scaleTo: w,
		scaleBy: T,
		setScaleExtent: E,
		setTranslateExtent: D,
		syncViewport: S,
		setClickDistance: O
	};
}
var qs;
(function(e) {
	e.Line = "line", e.Handle = "handle";
})(qs ||= {});
function Js({ width: e, prevWidth: t, height: n, prevHeight: r, affectsX: i, affectsY: a }) {
	let o = e - t, s = n - r, c = [o > 0 ? 1 : o < 0 ? -1 : 0, s > 0 ? 1 : s < 0 ? -1 : 0];
	return o && i && (c[0] *= -1), s && a && (c[1] *= -1), c;
}
function Ys(e) {
	return {
		isHorizontal: e.includes("right") || e.includes("left"),
		isVertical: e.includes("bottom") || e.includes("top"),
		affectsX: e.includes("left"),
		affectsY: e.includes("top")
	};
}
function Xs(e, t) {
	return Math.max(0, t - e);
}
function Zs(e, t) {
	return Math.max(0, e - t);
}
function Qs(e, t, n) {
	return Math.max(0, t - e, e - n);
}
function $s(e, t) {
	return e ? !t : t;
}
function ec(e, t, n, r, i, a, o, s) {
	let { affectsX: c, affectsY: l } = t, { isHorizontal: u, isVertical: d } = t, f = u && d, { xSnapped: p, ySnapped: m } = n, { minWidth: h, maxWidth: g, minHeight: _, maxHeight: v } = r, { x: y, y: b, width: x, height: S, aspectRatio: C } = e, w = Math.floor(u ? p - e.pointerX : 0), T = Math.floor(d ? m - e.pointerY : 0), E = x + (c ? -w : w), D = S + (l ? -T : T), O = -a[0] * x, k = -a[1] * S, A = Qs(E, h, g), j = Qs(D, _, v);
	if (o) {
		let e = 0, t = 0;
		c && w < 0 ? e = Xs(y + w + O, o[0][0]) : !c && w > 0 && (e = Zs(y + E + O, o[1][0])), l && T < 0 ? t = Xs(b + T + k, o[0][1]) : !l && T > 0 && (t = Zs(b + D + k, o[1][1])), A = Math.max(A, e), j = Math.max(j, t);
	}
	if (s) {
		let e = 0, t = 0;
		c && w > 0 ? e = Zs(y + w, s[0][0]) : !c && w < 0 && (e = Xs(y + E, s[1][0])), l && T > 0 ? t = Zs(b + T, s[0][1]) : !l && T < 0 && (t = Xs(b + D, s[1][1])), A = Math.max(A, e), j = Math.max(j, t);
	}
	if (i) {
		if (u) {
			let e = Qs(E / C, _, v) * C;
			if (A = Math.max(A, e), o) {
				let e = 0;
				e = !c && !l || c && !l && f ? Zs(b + k + E / C, o[1][1]) * C : Xs(b + k + (c ? w : -w) / C, o[0][1]) * C, A = Math.max(A, e);
			}
			if (s) {
				let e = 0;
				e = !c && !l || c && !l && f ? Xs(b + E / C, s[1][1]) * C : Zs(b + (c ? w : -w) / C, s[0][1]) * C, A = Math.max(A, e);
			}
		}
		if (d) {
			let e = Qs(D * C, h, g) / C;
			if (j = Math.max(j, e), o) {
				let e = 0;
				e = !c && !l || l && !c && f ? Zs(y + D * C + O, o[1][0]) / C : Xs(y + (l ? T : -T) * C + O, o[0][0]) / C, j = Math.max(j, e);
			}
			if (s) {
				let e = 0;
				e = !c && !l || l && !c && f ? Xs(y + D * C, s[1][0]) / C : Zs(y + (l ? T : -T) * C, s[0][0]) / C, j = Math.max(j, e);
			}
		}
	}
	T += T < 0 ? j : -j, w += w < 0 ? A : -A, i && (f ? E > D * C ? T = ($s(c, l) ? -w : w) / C : w = ($s(c, l) ? -T : T) * C : u ? (T = w / C, l = c) : (w = T * C, c = l));
	let M = c ? y + w : y, ee = l ? b + T : b;
	return {
		width: x + (c ? -w : w),
		height: S + (l ? -T : T),
		x: a[0] * w * (c ? -1 : 1) + M,
		y: a[1] * T * (l ? -1 : 1) + ee
	};
}
var tc = {
	width: 0,
	height: 0,
	x: 0,
	y: 0
}, nc = {
	...tc,
	pointerX: 0,
	pointerY: 0,
	aspectRatio: 1
};
function rc(e, t, n) {
	let r = t.position.x + e.position.x, i = t.position.y + e.position.y, a = e.measured.width ?? 0, o = e.measured.height ?? 0, s = n[0] * a, c = n[1] * o;
	return [[r - s, i - c], [r + a - s, i + o - c]];
}
function ic({ domNode: e, nodeId: t, getStoreItems: n, onChange: r, onEnd: i }) {
	let a = Vt(e), o = {
		controlDirection: Ys("bottom-right"),
		boundaries: {
			minWidth: 0,
			minHeight: 0,
			maxWidth: Number.MAX_VALUE,
			maxHeight: Number.MAX_VALUE
		},
		resizeDirection: void 0,
		keepAspectRatio: !1
	};
	function s({ controlPosition: e, boundaries: s, keepAspectRatio: c, resizeDirection: l, onResizeStart: u, onResize: d, onResizeEnd: f, shouldResize: p }) {
		let m = { ...tc }, h = { ...nc };
		o = {
			boundaries: s,
			resizeDirection: l,
			keepAspectRatio: c,
			controlDirection: Ys(e)
		};
		let g, _ = null, v = [], y, b, x, S = nn().on("start", (e) => {
			let { nodeLookup: r, transform: i, snapGrid: a, snapToGrid: o, nodeOrigin: s, paneDomNode: c } = n();
			if (g = r.get(t), !g) return;
			_ = c?.getBoundingClientRect() ?? null;
			let { xSnapped: l, ySnapped: d } = yo(e.sourceEvent, {
				transform: i,
				snapGrid: a,
				snapToGrid: o,
				containerBounds: _
			});
			m = {
				width: g.measured.width ?? 0,
				height: g.measured.height ?? 0,
				x: g.position.x ?? 0,
				y: g.position.y ?? 0
			}, h = {
				...m,
				pointerX: l,
				pointerY: d,
				aspectRatio: m.width / m.height
			}, y = void 0, b = uo(g.extent) ? g.extent : void 0, g.parentId && (g.extent === "parent" || g.expandParent) && (y = r.get(g.parentId)), y && g.extent === "parent" && (b = [[0, 0], [y.measured.width, y.measured.height]]), v = [], x = void 0;
			for (let [e, n] of r) if (n.parentId === t && (v.push({
				id: e,
				position: { ...n.position },
				extent: n.extent
			}), n.extent === "parent" || n.expandParent)) {
				let e = rc(n, g, n.origin ?? s);
				x = x ? [[Math.min(e[0][0], x[0][0]), Math.min(e[0][1], x[0][1])], [Math.max(e[1][0], x[1][0]), Math.max(e[1][1], x[1][1])]] : e;
			}
			u?.(e, { ...m });
		}).on("drag", (e) => {
			let { transform: t, snapGrid: i, snapToGrid: a, nodeOrigin: s } = n(), c = yo(e.sourceEvent, {
				transform: t,
				snapGrid: i,
				snapToGrid: a,
				containerBounds: _
			}), l = [];
			if (!g) return;
			let { x: u, y: f, width: S, height: C } = m, w = {}, T = g.origin ?? s, { width: E, height: D, x: O, y: k } = ec(h, o.controlDirection, c, o.boundaries, o.keepAspectRatio, T, b, x), A = E !== S, j = D !== C, M = O !== u && A, ee = k !== f && j;
			if (!M && !ee && !A && !j) return;
			let N = {
				prevValues: { ...m },
				startX: h.x,
				startY: h.y,
				childNodes: v.map((e) => ({
					...e,
					position: { ...e.position }
				}))
			};
			if ((M || ee || T[0] === 1 || T[1] === 1) && (w.x = M ? O : m.x, w.y = ee ? k : m.y, m.x = w.x, m.y = w.y, v.length > 0)) {
				let e = O - u, t = k - f;
				for (let n of v) n.position = {
					x: n.position.x - e + T[0] * (E - S),
					y: n.position.y - t + T[1] * (D - C)
				}, l.push(n);
			}
			if ((A || j) && (w.width = A && (!o.resizeDirection || o.resizeDirection === "horizontal") ? E : m.width, w.height = j && (!o.resizeDirection || o.resizeDirection === "vertical") ? D : m.height, m.width = w.width, m.height = w.height), y && g.expandParent) {
				let e = T[0] * (w.width ?? 0);
				w.x && w.x < e && (m.x = e, h.x -= w.x - e);
				let t = T[1] * (w.height ?? 0);
				w.y && w.y < t && (m.y = t, h.y -= w.y - t);
			}
			let te = Js({
				width: m.width,
				prevWidth: S,
				height: m.height,
				prevHeight: C,
				affectsX: o.controlDirection.affectsX,
				affectsY: o.controlDirection.affectsY
			}), P = {
				...m,
				direction: te
			};
			p?.(e, P) === !1 ? (m = N.prevValues, h.x = N.startX, h.y = N.startY, v.forEach((e, t) => {
				e.position = N.childNodes[t].position;
			})) : (d?.(e, P), r(w, l));
		}).on("end", (e) => {
			f?.(e, { ...m }), i?.({ ...m });
		});
		a.call(S);
	}
	function c() {
		a.on(".drag", null);
	}
	return {
		update: s,
		destroy: c
	};
}
// node_modules/use-sync-external-store/cjs/use-sync-external-store-shim.production.js
var ac = /* @__PURE__ */ o(((e) => {
	var t = u();
	function n(e, t) {
		return e === t && (e !== 0 || 1 / e == 1 / t) || e !== e && t !== t;
	}
	var r = typeof Object.is == "function" ? Object.is : n, i = t.useState, a = t.useEffect, o = t.useLayoutEffect, s = t.useDebugValue;
	function c(e, t) {
		var n = t(), r = i({ inst: {
			value: n,
			getSnapshot: t
		} }), c = r[0].inst, u = r[1];
		return o(function() {
			c.value = n, c.getSnapshot = t, l(c) && u({ inst: c });
		}, [
			e,
			n,
			t
		]), a(function() {
			return l(c) && u({ inst: c }), e(function() {
				l(c) && u({ inst: c });
			});
		}, [e]), s(n), n;
	}
	function l(e) {
		var t = e.getSnapshot;
		e = e.value;
		try {
			var n = t();
			return !r(e, n);
		} catch {
			return !0;
		}
	}
	function d(e, t) {
		return t();
	}
	var f = typeof window > "u" || window.document === void 0 || window.document.createElement === void 0 ? d : c;
	e.useSyncExternalStore = t.useSyncExternalStore === void 0 ? f : t.useSyncExternalStore;
})), oc = /* @__PURE__ */ o(((e, t) => {
	t.exports = ac();
})), sc = /* @__PURE__ */ o(((e) => {
	var t = u(), n = oc();
	function r(e, t) {
		return e === t && (e !== 0 || 1 / e == 1 / t) || e !== e && t !== t;
	}
	var i = typeof Object.is == "function" ? Object.is : r, a = n.useSyncExternalStore, o = t.useRef, s = t.useEffect, c = t.useMemo, l = t.useDebugValue;
	e.useSyncExternalStoreWithSelector = function(e, t, n, r, u) {
		var d = o(null);
		if (d.current === null) {
			var f = {
				hasValue: !1,
				value: null
			};
			d.current = f;
		} else f = d.current;
		d = c(function() {
			function e(e) {
				if (!a) {
					if (a = !0, o = e, e = r(e), u !== void 0 && f.hasValue) {
						var t = f.value;
						if (u(t, e)) return s = t;
					}
					return s = e;
				}
				if (t = s, i(o, e)) return t;
				var n = r(e);
				return u !== void 0 && u(t, n) ? (o = e, t) : (o = e, s = n);
			}
			var a = !1, o, s, c = n === void 0 ? null : n;
			return [function() {
				return e(t());
			}, c === null ? void 0 : function() {
				return e(c());
			}];
		}, [
			t,
			n,
			r,
			u
		]);
		var p = a(e, d[0], d[1]);
		return s(function() {
			f.hasValue = !0, f.value = p;
		}, [p]), l(p), p;
	};
})), cc = /* @__PURE__ */ c((/* @__PURE__ */ o(((e, t) => {
	t.exports = sc();
})))(), 1), lc = (e) => {
	let t, n = /* @__PURE__ */ new Set(), r = (e, r) => {
		let i = typeof e == "function" ? e(t) : e;
		if (!Object.is(i, t)) {
			let e = t;
			t = r ?? (typeof i != "object" || !i) ? i : Object.assign({}, t, i), n.forEach((n) => n(t, e));
		}
	}, i = () => t, a = {
		setState: r,
		getState: i,
		getInitialState: () => o,
		subscribe: (e) => (n.add(e), () => n.delete(e)),
		destroy: () => {
			n.clear();
		}
	}, o = t = e(r, i, a);
	return a;
}, uc = (e) => e ? lc(e) : lc, { useDebugValue: dc } = y.default, { useSyncExternalStoreWithSelector: fc } = cc.default, pc = (e) => e;
function mc(e, t = pc, n) {
	let r = fc(e.subscribe, e.getState, e.getServerState || e.getInitialState, t, n);
	return dc(r), r;
}
var hc = (e, t) => {
	let n = uc(e), r = (e, r = t) => mc(n, e, r);
	return Object.assign(r, n), r;
}, gc = (e, t) => e ? hc(e, t) : hc;
// node_modules/zustand/esm/shallow.mjs
function _c(e, t) {
	if (Object.is(e, t)) return !0;
	if (typeof e != "object" || !e || typeof t != "object" || !t) return !1;
	if (e instanceof Map && t instanceof Map) {
		if (e.size !== t.size) return !1;
		for (let [n, r] of e) if (!Object.is(r, t.get(n))) return !1;
		return !0;
	}
	if (e instanceof Set && t instanceof Set) {
		if (e.size !== t.size) return !1;
		for (let n of e) if (!t.has(n)) return !1;
		return !0;
	}
	let n = Object.keys(e);
	if (n.length !== Object.keys(t).length) return !1;
	for (let r of n) if (!Object.prototype.hasOwnProperty.call(t, r) || !Object.is(e[r], t[r])) return !1;
	return !0;
}
m();
var vc = (0, y.createContext)(null), yc = vc.Provider, bc = ya.error001("react");
function W(e, t) {
	let n = (0, y.useContext)(vc);
	if (n === null) throw Error(bc);
	return mc(n, e, t);
}
function G() {
	let e = (0, y.useContext)(vc);
	if (e === null) throw Error(bc);
	return (0, y.useMemo)(() => ({
		getState: e.getState,
		setState: e.setState,
		subscribe: e.subscribe
	}), [e]);
}
var xc = { display: "none" }, Sc = {
	position: "absolute",
	width: 1,
	height: 1,
	margin: -1,
	border: 0,
	padding: 0,
	overflow: "hidden",
	clip: "rect(0px, 0px, 0px, 0px)",
	clipPath: "inset(100%)"
}, Cc = "react-flow__node-desc", wc = "react-flow__edge-desc", Tc = "react-flow__aria-live", Ec = (e) => e.ariaLiveMessage, Dc = (e) => e.ariaLabelConfig;
function Oc({ rfId: e }) {
	let t = W(Ec);
	return (0, x.jsx)("div", {
		id: `${Tc}-${e}`,
		"aria-live": "assertive",
		"aria-atomic": "true",
		style: Sc,
		children: t
	});
}
function kc({ rfId: e, disableKeyboardA11y: t }) {
	let n = W(Dc);
	return (0, x.jsxs)(x.Fragment, { children: [
		(0, x.jsx)("div", {
			id: `${Cc}-${e}`,
			style: xc,
			children: t ? n["node.a11yDescription.default"] : n["node.a11yDescription.keyboardDisabled"]
		}),
		(0, x.jsx)("div", {
			id: `${wc}-${e}`,
			style: xc,
			children: n["edge.a11yDescription.default"]
		}),
		!t && (0, x.jsx)(Oc, { rfId: e })
	] });
}
var Ac = (0, y.forwardRef)(({ position: e = "top-left", children: t, className: n, style: r, ...i }, a) => {
	let o = `${e}`.split("-");
	return (0, x.jsx)("div", {
		className: S([
			"react-flow__panel",
			n,
			...o
		]),
		style: r,
		ref: a,
		...i,
		children: t
	});
});
Ac.displayName = "Panel";
var jc = "https://reactflow.dev?utm_source=attribution";
function Mc({ proOptions: e, position: t = "bottom-right" }) {
	return (0, y.useEffect)(() => {}, []), e?.hideAttribution ? null : (0, x.jsx)(Ac, {
		position: t,
		className: "react-flow__attribution",
		"data-message": `Please only hide this attribution when you are subscribed to React Flow Pro: ${jc}`,
		children: (0, x.jsx)("a", {
			href: jc,
			target: "_blank",
			rel: "noopener noreferrer",
			"aria-label": "React Flow attribution",
			children: "React Flow"
		})
	});
}
var Nc = (e) => {
	let t = [], n = [];
	for (let [, n] of e.nodeLookup) n.selected && t.push(n.internals.userNode);
	for (let [, t] of e.edgeLookup) t.selected && n.push(t);
	return {
		selectedNodes: t,
		selectedEdges: n
	};
}, Pc = (e) => e.id;
function Fc(e, t) {
	return _c(e.selectedNodes.map(Pc), t.selectedNodes.map(Pc)) && _c(e.selectedEdges.map(Pc), t.selectedEdges.map(Pc));
}
function Ic({ onSelectionChange: e }) {
	let t = G(), { selectedNodes: n, selectedEdges: r } = W(Nc, Fc);
	return (0, y.useEffect)(() => {
		let i = {
			nodes: n,
			edges: r
		};
		e?.(i), t.getState().onSelectionChangeHandlers.forEach((e) => e(i));
	}, [
		n,
		r,
		e
	]), null;
}
var Lc = (e) => !!e.onSelectionChangeHandlers;
function Rc({ onSelectionChange: e }) {
	let t = W(Lc);
	return e || t ? (0, x.jsx)(Ic, { onSelectionChange: e }) : null;
}
var zc = [0, 0], Bc = {
	x: 0,
	y: 0,
	zoom: 1
}, Vc = [.../* @__PURE__ */ "nodes.edges.defaultNodes.defaultEdges.onConnect.onConnectStart.onConnectEnd.onClickConnectStart.onClickConnectEnd.nodesDraggable.autoPanOnNodeFocus.nodesConnectable.nodesFocusable.edgesFocusable.edgesReconnectable.elevateNodesOnSelect.elevateEdgesOnSelect.minZoom.maxZoom.nodeExtent.onNodesChange.onEdgesChange.elementsSelectable.connectionMode.snapGrid.snapToGrid.translateExtent.connectOnClick.defaultEdgeOptions.fitView.fitViewOptions.onNodesDelete.onEdgesDelete.onDelete.onNodeDrag.onNodeDragStart.onNodeDragStop.onSelectionDrag.onSelectionDragStart.onSelectionDragStop.onMoveStart.onMove.onMoveEnd.noPanClassName.nodeOrigin.autoPanOnConnect.autoPanOnNodeDrag.onError.connectionRadius.isValidConnection.selectNodesOnDrag.nodeDragThreshold.connectionDragThreshold.onBeforeDelete.debug.autoPanSpeed.ariaLabelConfig.zIndexMode".split("."), "rfId"], Hc = (e) => ({
	setNodes: e.setNodes,
	setEdges: e.setEdges,
	setMinZoom: e.setMinZoom,
	setMaxZoom: e.setMaxZoom,
	setTranslateExtent: e.setTranslateExtent,
	setNodeExtent: e.setNodeExtent,
	reset: e.reset,
	setDefaultNodesAndEdges: e.setDefaultNodesAndEdges
}), Uc = {
	translateExtent: ba,
	nodeOrigin: zc,
	minZoom: .5,
	maxZoom: 2,
	elementsSelectable: !0,
	noPanClassName: "nopan",
	rfId: "1"
};
function Wc(e) {
	let { setNodes: t, setEdges: n, setMinZoom: r, setMaxZoom: i, setTranslateExtent: a, setNodeExtent: o, reset: s, setDefaultNodesAndEdges: c } = W(Hc, _c), l = G();
	(0, y.useEffect)(() => (c(e.defaultNodes, e.defaultEdges), () => {
		u.current = Uc, s();
	}), []);
	let u = (0, y.useRef)(Uc);
	return (0, y.useEffect)(() => {
		for (let s of Vc) {
			let c = e[s];
			c !== u.current[s] && e[s] !== void 0 && (s === "nodes" ? t(c) : s === "edges" ? n(c) : s === "minZoom" ? r(c) : s === "maxZoom" ? i(c) : s === "translateExtent" ? a(c) : s === "nodeExtent" ? o(c) : s === "ariaLabelConfig" ? l.setState({ ariaLabelConfig: _o(c) }) : s === "fitView" ? l.setState({ fitViewQueued: c }) : s === "fitViewOptions" ? l.setState({ fitViewOptions: c }) : l.setState({ [s]: c }));
		}
		u.current = e;
	}, Vc.map((t) => e[t])), null;
}
function Gc() {
	return typeof window > "u" || !window.matchMedia ? null : window.matchMedia("(prefers-color-scheme: dark)");
}
function Kc(e) {
	let [t, n] = (0, y.useState)(e === "system" ? null : e);
	return (0, y.useEffect)(() => {
		if (e !== "system") {
			n(e);
			return;
		}
		let t = Gc(), r = () => n(t?.matches ? "dark" : "light");
		return r(), t?.addEventListener("change", r), () => {
			t?.removeEventListener("change", r);
		};
	}, [e]), t === null ? Gc()?.matches ? "dark" : "light" : t;
}
var qc = typeof document < "u" ? document : null;
function Jc(e = null, t = {
	target: qc,
	actInsideInputWithModifier: !0
}) {
	let [n, r] = (0, y.useState)(!1), i = (0, y.useRef)(!1), a = (0, y.useRef)(/* @__PURE__ */ new Set([])), [o, s] = (0, y.useMemo)(() => {
		if (e !== null) {
			let t = (Array.isArray(e) ? e : [e]).filter((e) => typeof e == "string").map((e) => e.replace(/\+/g, "\n").replace("\n\n", "\n+").split("\n"));
			return [t, t.reduce((e, t) => e.concat(...t), [])];
		}
		return [[], []];
	}, [e]);
	return (0, y.useEffect)(() => {
		let n = t?.target ?? qc, c = t?.actInsideInputWithModifier ?? !0;
		if (e !== null) {
			let e = (e) => {
				if (i.current = e.ctrlKey || e.metaKey || e.shiftKey || e.altKey, (!i.current || i.current && !c) && Co(e)) return !1;
				let n = Xc(e.code, s);
				if (a.current.add(e[n]), Yc(o, a.current, !1)) {
					let n = e.composedPath?.()?.[0] || e.target, a = n?.nodeName === "BUTTON" || n?.nodeName === "A";
					t.preventDefault !== !1 && (i.current || !a) && e.preventDefault(), r(!0);
				}
			}, l = (e) => {
				let t = Xc(e.code, s);
				Yc(o, a.current, !0) ? (r(!1), a.current.clear()) : a.current.delete(e[t]), e.key === "Meta" && a.current.clear(), i.current = !1;
			}, u = () => {
				a.current.clear(), r(!1);
			};
			return n?.addEventListener("keydown", e), n?.addEventListener("keyup", l), window.addEventListener("blur", u), window.addEventListener("contextmenu", u), () => {
				n?.removeEventListener("keydown", e), n?.removeEventListener("keyup", l), window.removeEventListener("blur", u), window.removeEventListener("contextmenu", u);
			};
		}
	}, [e, r]), n;
}
function Yc(e, t, n) {
	return e.filter((e) => n || e.length === t.size).some((e) => e.every((e) => t.has(e)));
}
function Xc(e, t) {
	return t.includes(e) ? "code" : "key";
}
var Zc = () => {
	let e = G();
	return (0, y.useMemo)(() => ({
		zoomIn: async (t) => {
			let { panZoom: n } = e.getState();
			return n ? n.scaleBy(1.2, t) : !1;
		},
		zoomOut: async (t) => {
			let { panZoom: n } = e.getState();
			return n ? n.scaleBy(1 / 1.2, t) : !1;
		},
		zoomTo: async (t, n) => {
			let { panZoom: r } = e.getState();
			return r ? r.scaleTo(t, n) : !1;
		},
		getZoom: () => e.getState().transform[2],
		setViewport: async (t, n) => {
			let { transform: [r, i, a], panZoom: o } = e.getState();
			return o ? (await o.setViewport({
				x: t.x ?? r,
				y: t.y ?? i,
				zoom: t.zoom ?? a
			}, n), !0) : !1;
		},
		getViewport: () => {
			let [t, n, r] = e.getState().transform;
			return {
				x: t,
				y: n,
				zoom: r
			};
		},
		setCenter: async (t, n, r) => e.getState().setCenter(t, n, r),
		fitBounds: async (t, n) => {
			let { width: r, height: i, minZoom: a, maxZoom: o, panZoom: s } = e.getState(), c = co(t, r, i, a, o, n?.padding ?? .1);
			return s ? (await s.setViewport(c, {
				duration: n?.duration,
				ease: n?.ease,
				interpolate: n?.interpolate
			}), !0) : !1;
		},
		screenToFlowPosition: (t, n = {}) => {
			let { transform: r, snapGrid: i, snapToGrid: a, domNode: o } = e.getState();
			if (!o) return t;
			let { x: s, y: c } = o.getBoundingClientRect(), l = {
				x: t.x - s,
				y: t.y - c
			}, u = n.snapGrid ?? i;
			return ro(l, r, n.snapToGrid ?? a, u);
		},
		flowToScreenPosition: (t) => {
			let { transform: n, domNode: r } = e.getState();
			if (!r) return t;
			let { x: i, y: a } = r.getBoundingClientRect(), o = io(t, n);
			return {
				x: o.x + i,
				y: o.y + a
			};
		}
	}), []);
};
function Qc(e, t) {
	let n = [], r = /* @__PURE__ */ new Map(), i = [];
	for (let t of e) if (t.type === "add") {
		i.push(t);
		continue;
	} else if (t.type === "remove" || t.type === "replace") r.set(t.id, [t]);
	else {
		let e = r.get(t.id);
		e ? e.push(t) : r.set(t.id, [t]);
	}
	for (let e of t) {
		let t = r.get(e.id);
		if (!t) {
			n.push(e);
			continue;
		}
		if (t[0].type === "remove") continue;
		if (t[0].type === "replace") {
			n.push({ ...t[0].item });
			continue;
		}
		let i = { ...e };
		for (let e of t) $c(e, i);
		n.push(i);
	}
	return i.length && i.forEach((e) => {
		e.index === void 0 ? n.push({ ...e.item }) : n.splice(e.index, 0, { ...e.item });
	}), n;
}
function $c(e, t) {
	switch (e.type) {
		case "select":
			t.selected = e.selected;
			break;
		case "position":
			e.position !== void 0 && (t.position = e.position), e.dragging !== void 0 && (t.dragging = e.dragging);
			break;
		case "dimensions": e.dimensions !== void 0 && (t.measured = { ...e.dimensions }, e.setAttributes && ((e.setAttributes === !0 || e.setAttributes === "width") && (t.width = e.dimensions.width), (e.setAttributes === !0 || e.setAttributes === "height") && (t.height = e.dimensions.height))), typeof e.resizing == "boolean" && (t.resizing = e.resizing);
	}
}
function el(e, t) {
	return Qc(e, t);
}
function tl(e, t) {
	return Qc(e, t);
}
function nl(e, t) {
	return {
		id: e,
		type: "select",
		selected: t
	};
}
function rl(e, t = /* @__PURE__ */ new Set()) {
	let n = [];
	for (let [r, i] of e) {
		let e = t.has(r);
		(i.selected !== void 0 || e) && i.selected !== e && n.push(nl(i.id, e));
	}
	return n;
}
function il({ items: e = [], lookup: t }) {
	let n = [], r = new Map(e.map((e) => [e.id, e]));
	for (let [r, i] of e.entries()) {
		let e = t.get(i.id), a = e?.internals?.userNode ?? e;
		a !== void 0 && a !== i && n.push({
			id: i.id,
			item: i,
			type: "replace"
		}), a === void 0 && n.push({
			item: i,
			type: "add",
			index: r
		});
	}
	for (let [e] of t) r.get(e) === void 0 && n.push({
		id: e,
		type: "remove"
	});
	return n;
}
function al(e) {
	return {
		id: e.id,
		type: "remove"
	};
}
var ol = to("React Flow", "https://reactflow.dev/");
function sl(e, t, n = {}) {
	return Io(e, t, {
		...n,
		onError: n.onError ?? ol
	});
}
var cl = (e) => ja(e), ll = (e) => Aa(e);
function ul(e) {
	return (0, y.forwardRef)(e);
}
var dl = typeof window < "u" ? y.useLayoutEffect : y.useEffect;
function fl(e) {
	let [t, n] = (0, y.useState)(BigInt(0)), [r] = (0, y.useState)(() => pl(() => n((e) => e + BigInt(1))));
	return dl(() => {
		let t = r.get();
		t.length && (e(t), r.reset());
	}, [t]), r;
}
function pl(e) {
	let t = [];
	return {
		get: () => t,
		reset: () => {
			t = [];
		},
		push: (n) => {
			t.push(n), e();
		}
	};
}
var ml = (0, y.createContext)(null);
function hl({ children: e }) {
	let t = G(), n = fl((0, y.useCallback)((e) => {
		let { nodes: n = [], setNodes: r, hasDefaultNodes: i, onNodesChange: a, nodeLookup: o, fitViewQueued: s, onNodesChangeMiddlewareMap: c } = t.getState(), l = n;
		for (let t of e) l = typeof t == "function" ? t(l) : t;
		let u = il({
			items: l,
			lookup: o
		});
		for (let e of c.values()) u = e(u);
		i && r(l), u.length > 0 ? a?.(u) : s && window.requestAnimationFrame(() => {
			let { fitViewQueued: e, nodes: n, setNodes: r } = t.getState();
			e && r(n);
		});
	}, [])), r = fl((0, y.useCallback)((e) => {
		let { edges: n = [], setEdges: r, hasDefaultEdges: i, onEdgesChange: a, edgeLookup: o } = t.getState(), s = n;
		for (let t of e) s = typeof t == "function" ? t(s) : t;
		i ? r(s) : a && a(il({
			items: s,
			lookup: o
		}));
	}, [])), i = (0, y.useMemo)(() => ({
		nodeQueue: n,
		edgeQueue: r
	}), []);
	return (0, x.jsx)(ml.Provider, {
		value: i,
		children: e
	});
}
function gl() {
	let e = (0, y.useContext)(ml);
	if (!e) throw Error("useBatchContext must be used within a BatchProvider");
	return e;
}
var _l = (e) => !!e.panZoom;
function vl() {
	let e = Zc(), t = G(), n = gl(), r = W(_l), i = (0, y.useMemo)(() => {
		let e = (e) => t.getState().nodeLookup.get(e), r = (e) => {
			n.nodeQueue.push(e);
		}, i = (e) => {
			n.edgeQueue.push(e);
		}, a = (e) => {
			let { nodeLookup: n, nodeOrigin: r } = t.getState(), i = cl(e) ? e : n.get(e.id), a = i.parentId ? mo(i.position, i.measured, i.parentId, n, r) : i.position;
			return Ja({
				...i,
				position: a,
				width: i.measured?.width ?? i.width,
				height: i.measured?.height ?? i.height
			}, i.parentId ? void 0 : r);
		}, o = (e, t, n = { replace: !1 }) => {
			r((r) => r.map((r) => {
				if (r.id === e) {
					let e = typeof t == "function" ? t(r) : t;
					return n.replace && cl(e) ? e : {
						...r,
						...e
					};
				}
				return r;
			}));
		}, s = (e, t, n = { replace: !1 }) => {
			i((r) => r.map((r) => {
				if (r.id === e) {
					let e = typeof t == "function" ? t(r) : t;
					return n.replace && ll(e) ? e : {
						...r,
						...e
					};
				}
				return r;
			}));
		};
		return {
			getNodes: () => t.getState().nodes.map((e) => ({ ...e })),
			getNode: (t) => e(t)?.internals.userNode,
			getInternalNode: e,
			getEdges: () => {
				let { edges: e = [] } = t.getState();
				return e.map((e) => ({ ...e }));
			},
			getEdge: (e) => t.getState().edgeLookup.get(e),
			setNodes: r,
			setEdges: i,
			addNodes: (e) => {
				let t = Array.isArray(e) ? e : [e];
				n.nodeQueue.push((e) => [...e, ...t]);
			},
			addEdges: (e) => {
				let t = Array.isArray(e) ? e : [e];
				n.edgeQueue.push((e) => [...e, ...t]);
			},
			toObject: () => {
				let { nodes: e = [], edges: n = [], transform: r } = t.getState(), [i, a, o] = r;
				return {
					nodes: e.map((e) => ({ ...e })),
					edges: n.map((e) => ({ ...e })),
					viewport: {
						x: i,
						y: a,
						zoom: o
					}
				};
			},
			deleteElements: async ({ nodes: e = [], edges: n = [] }) => {
				let { nodes: r, edges: i, onNodesDelete: a, onEdgesDelete: o, triggerNodeChanges: s, triggerEdgeChanges: c, onDelete: l, onBeforeDelete: u } = t.getState(), { nodes: d, edges: f } = await Va({
					nodesToRemove: e,
					edgesToRemove: n,
					nodes: r,
					edges: i,
					onBeforeDelete: u
				}), p = f.length > 0, m = d.length > 0;
				if (p) {
					let e = f.map(al);
					o?.(f), c(e);
				}
				if (m) {
					let e = d.map(al);
					a?.(d), s(e);
				}
				return (m || p) && l?.({
					nodes: d,
					edges: f
				}), {
					deletedNodes: d,
					deletedEdges: f
				};
			},
			getIntersectingNodes: (e, n = !0, r) => {
				let i = $a(e), o = i ? e : a(e), s = r !== void 0;
				if (!o) return [];
				let { nodeOrigin: c } = t.getState();
				return (r || t.getState().nodes).filter((r) => {
					let a = t.getState().nodeLookup.get(r.id);
					if (a && !i && (r.id === e.id || !a.internals.positionAbsolute)) return !1;
					let l = Ja(s ? r : a, c), u = Qa(l, o);
					return n && u > 0 || u >= l.width * l.height || u >= o.width * o.height;
				});
			},
			isNodeIntersecting: (e, t, n = !0) => {
				let r = $a(e) ? e : a(e);
				if (!r) return !1;
				let i = Qa(r, t);
				return n && i > 0 || i >= t.width * t.height || i >= r.width * r.height;
			},
			updateNode: o,
			updateNodeData: (e, t, n = { replace: !1 }) => {
				o(e, (e) => {
					let r = typeof t == "function" ? t(e) : t;
					return n.replace ? {
						...e,
						data: r
					} : {
						...e,
						data: {
							...e.data,
							...r
						}
					};
				}, n);
			},
			updateEdge: s,
			updateEdgeData: (e, t, n = { replace: !1 }) => {
				s(e, (e) => {
					let r = typeof t == "function" ? t(e) : t;
					return n.replace ? {
						...e,
						data: r
					} : {
						...e,
						data: {
							...e.data,
							...r
						}
					};
				}, n);
			},
			getNodesBounds: (e) => {
				let { nodeLookup: n, nodeOrigin: r } = t.getState();
				return Pa(e, {
					nodeLookup: n,
					nodeOrigin: r
				});
			},
			getHandleConnections: ({ type: e, id: n, nodeId: r }) => Array.from(t.getState().connectionLookup.get(`${r}-${e}${n ? `-${n}` : ""}`)?.values() ?? []),
			getNodeConnections: ({ type: e, handleId: n, nodeId: r }) => Array.from(t.getState().connectionLookup.get(`${r}${e ? n ? `-${e}-${n}` : `-${e}` : ""}`)?.values() ?? []),
			fitView: async (e) => {
				let r = t.getState().fitViewResolver ?? go();
				return t.setState({
					fitViewQueued: !0,
					fitViewOptions: e,
					fitViewResolver: r
				}), n.nodeQueue.push((e) => [...e]), r.promise;
			}
		};
	}, []);
	return (0, y.useMemo)(() => ({
		...i,
		...e,
		viewportInitialized: r
	}), [r]);
}
var yl = (e) => e.selected, bl = typeof window < "u" ? window : void 0;
function xl({ deleteKeyCode: e, multiSelectionKeyCode: t }) {
	let n = G(), { deleteElements: r } = vl(), i = Jc(e, { actInsideInputWithModifier: !1 }), a = Jc(t, { target: bl });
	(0, y.useEffect)(() => {
		if (i) {
			let { edges: e, nodes: t } = n.getState();
			r({
				nodes: t.filter(yl),
				edges: e.filter(yl)
			}), n.setState({ nodesSelectionActive: !1 });
		}
	}, [i]), (0, y.useEffect)(() => {
		n.setState({ multiSelectionActive: a });
	}, [a]);
}
function Sl(e) {
	let t = G();
	(0, y.useEffect)(() => {
		let n = () => {
			if (!e.current || !(e.current.checkVisibility?.() ?? !0)) return !1;
			let n = bo(e.current);
			(n.height === 0 || n.width === 0) && t.getState().onError?.("004", ya.error004()), t.setState({
				width: n.width || 500,
				height: n.height || 500
			});
		};
		if (e.current) {
			n(), window.addEventListener("resize", n);
			let t = new ResizeObserver(() => n());
			return t.observe(e.current), () => {
				window.removeEventListener("resize", n), t.disconnect();
			};
		}
	}, []);
}
var Cl = {
	position: "absolute",
	width: "100%",
	height: "100%",
	top: 0,
	left: 0
}, wl = (e) => ({
	userSelectionActive: e.userSelectionActive,
	lib: e.lib,
	connectionInProgress: e.connection.inProgress
});
function Tl({ onPaneContextMenu: e, zoomOnScroll: t = !0, zoomOnPinch: n = !0, panOnScroll: r = !1, panActivationKeyPressed: i, panOnScrollSpeed: a = .5, panOnScrollMode: o = wa.Free, zoomOnDoubleClick: s = !0, panOnDrag: c = !0, defaultViewport: l, translateExtent: u, minZoom: d, maxZoom: f, zoomActivationKeyCode: p, preventScrolling: m = !0, children: h, noWheelClassName: g, noPanClassName: _, onViewportChange: v, isControlledViewport: b, paneClickDistance: S, selectionOnDrag: C }) {
	let w = G(), T = (0, y.useRef)(null), { userSelectionActive: E, lib: D, connectionInProgress: O } = W(wl, _c), k = Jc(p), A = (0, y.useRef)();
	Sl(T);
	let j = (0, y.useCallback)((e) => {
		v?.({
			x: e[0],
			y: e[1],
			zoom: e[2]
		}), b || w.setState({ transform: e });
	}, [v, b]);
	return (0, y.useEffect)(() => {
		if (T.current) {
			A.current = Ks({
				domNode: T.current,
				minZoom: d,
				maxZoom: f,
				translateExtent: u,
				viewport: l,
				onDraggingChange: (e) => w.setState((t) => t.paneDragging === e ? t : { paneDragging: e }),
				onPanZoomStart: (e, t) => {
					let { onViewportChangeStart: n, onMoveStart: r } = w.getState();
					r?.(e, t), n?.(t);
				},
				onPanZoom: (e, t) => {
					let { onViewportChange: n, onMove: r } = w.getState();
					r?.(e, t), n?.(t);
				},
				onPanZoomEnd: (e, t) => {
					let { onViewportChangeEnd: n, onMoveEnd: r } = w.getState();
					r?.(e, t), n?.(t);
				}
			});
			let { x: e, y: t, zoom: n } = A.current.getViewport();
			return w.setState({
				panZoom: A.current,
				transform: [
					e,
					t,
					n
				],
				domNode: T.current.closest(".react-flow")
			}), () => {
				A.current?.destroy();
			};
		}
	}, []), (0, y.useEffect)(() => {
		A.current?.update({
			onPaneContextMenu: e,
			zoomOnScroll: t,
			zoomOnPinch: n,
			panOnScroll: r,
			panActivationKeyPressed: i,
			panOnScrollSpeed: a,
			panOnScrollMode: o,
			zoomOnDoubleClick: s,
			panOnDrag: c,
			zoomActivationKeyPressed: k,
			preventScrolling: m,
			noPanClassName: _,
			userSelectionActive: E,
			noWheelClassName: g,
			lib: D,
			onTransformChange: j,
			connectionInProgress: O,
			selectionOnDrag: C,
			paneClickDistance: S
		});
	}, [
		e,
		t,
		n,
		r,
		i,
		a,
		o,
		s,
		c,
		k,
		m,
		_,
		E,
		g,
		D,
		j,
		O,
		C,
		S
	]), (0, x.jsx)("div", {
		className: "react-flow__renderer",
		ref: T,
		style: Cl,
		children: h
	});
}
var El = (e) => ({
	userSelectionActive: e.userSelectionActive,
	userSelectionRect: e.userSelectionRect
});
function Dl() {
	let { userSelectionActive: e, userSelectionRect: t } = W(El, _c);
	return e && t ? (0, x.jsx)("div", {
		className: "react-flow__selection react-flow__container",
		style: {
			width: t.width,
			height: t.height,
			transform: `translate(${t.x}px, ${t.y}px)`
		}
	}) : null;
}
var K = (e, t) => (n) => {
	n.target === t.current && e?.(n);
}, q = (e) => ({
	userSelectionActive: e.userSelectionActive,
	elementsSelectable: e.elementsSelectable,
	dragging: e.paneDragging,
	panBy: e.panBy,
	autoPanSpeed: e.autoPanSpeed
});
function J({ isSelecting: e, selectionKeyPressed: t, selectionMode: n = Ta.Full, panOnDrag: r, autoPanOnSelection: i, paneClickDistance: a, selectionOnDrag: o, onSelectionStart: s, onSelectionEnd: c, onPaneClick: l, onPaneContextMenu: u, onPaneScroll: d, onPaneMouseEnter: f, onPaneMouseMove: p, onPaneMouseLeave: m, children: h }) {
	let g = (0, y.useRef)(0), _ = G(), { userSelectionActive: v, elementsSelectable: b, dragging: C, panBy: w, autoPanSpeed: T } = W(q, _c), E = b && (e || v), D = (0, y.useRef)(null), O = (0, y.useRef)(), k = (0, y.useRef)(/* @__PURE__ */ new Set()), A = (0, y.useRef)(/* @__PURE__ */ new Set()), j = (0, y.useRef)(!1), M = (0, y.useRef)(!1), ee = (0, y.useRef)({
		x: 0,
		y: 0
	}), N = (0, y.useRef)(!1), te = (e) => {
		if (M.current || j.current || _.getState().connection.inProgress) {
			M.current = !1, j.current = !1;
			return;
		}
		let { connectionClickStartHandle: t, connection: n, onClickConnectEnd: r, resetSelectedElements: i } = _.getState();
		if (t) {
			let t = structuredClone(n);
			delete t.inProgress, t.toPosition = t.toHandle ? t.toHandle.position : null, r?.(e.nativeEvent, t), _.setState({ connectionClickStartHandle: null });
		}
		l?.(e), i(), _.setState({ nodesSelectionActive: !1 });
	}, P = (e) => {
		Array.isArray(r) && r?.includes(2) ? e.preventDefault() : u?.(e);
	}, F = d ? (e) => d(e) : void 0, ne = (e) => {
		M.current &&= (e.stopPropagation(), !1);
	}, re = (n) => {
		if (n.pointerType === "touch" && r !== !1 && !t) return;
		let { domNode: i, transform: a } = _.getState();
		if (O.current = i?.getBoundingClientRect(), !O.current) return;
		let s = n.target === D.current;
		if (!s && n.target.closest(".nokey") || !e || !(o && s || t) || n.button !== 0 || !n.isPrimary) return;
		n.target?.setPointerCapture?.(n.pointerId), M.current = !1;
		let { x: c, y: l } = To(n.nativeEvent, O.current), u = ro({
			x: c,
			y: l
		}, a);
		_.setState({ userSelectionRect: {
			width: 0,
			height: 0,
			startX: u.x,
			startY: u.y,
			x: c,
			y: l
		} }), s || (n.stopPropagation(), n.preventDefault());
	};
	function I(e, t) {
		let { userSelectionRect: r } = _.getState();
		if (!r) return;
		let { transform: i, nodeLookup: a, edgeLookup: o, connectionLookup: s, triggerNodeChanges: c, triggerEdgeChanges: l, defaultEdgeOptions: u } = _.getState(), d = {
			x: r.startX,
			y: r.startY
		}, { x: f, y: p } = io(d, i), m = {
			startX: d.x,
			startY: d.y,
			x: e < f ? e : f,
			y: t < p ? t : p,
			width: Math.abs(e - f),
			height: Math.abs(t - p)
		}, h = k.current, g = A.current;
		k.current = new Set(Ia(a, m, i, n === Ta.Partial, !0).map((e) => e.id)), A.current = /* @__PURE__ */ new Set();
		let v = u?.selectable ?? !0;
		for (let e of k.current) {
			let t = s.get(e);
			if (t) for (let { edgeId: e } of t.values()) {
				let t = o.get(e);
				t && (t.selectable ?? v) && A.current.add(e);
			}
		}
		ho(h, k.current) || c(rl(a, k.current)), ho(g, A.current) || l(rl(o, A.current)), _.setState({
			userSelectionRect: m,
			userSelectionActive: !0,
			nodesSelectionActive: !1
		});
	}
	function L() {
		if (!i || !O.current) return;
		let [e, t] = Wa(ee.current, O.current, T);
		w({
			x: e,
			y: t
		}).then((e) => {
			if (!M.current || !e) {
				g.current = requestAnimationFrame(L);
				return;
			}
			let { x: t, y: n } = ee.current;
			I(t, n), g.current = requestAnimationFrame(L);
		});
	}
	let R = () => {
		cancelAnimationFrame(g.current), g.current = 0, N.current = !1;
	};
	(0, y.useEffect)(() => () => R(), []);
	let z = (e) => {
		let { userSelectionRect: n, transform: r, resetSelectedElements: i } = _.getState();
		if (!O.current || !n) return;
		let { x: o, y: c } = To(e.nativeEvent, O.current);
		ee.current = {
			x: o,
			y: c
		};
		let l = io({
			x: n.startX,
			y: n.startY
		}, r);
		if (!M.current) {
			let n = t ? 0 : a;
			if (Math.hypot(o - l.x, c - l.y) <= n) return;
			i(), s?.(e);
		}
		M.current = !0, N.current ||= (L(), !0), I(o, c);
	}, ie = (e) => {
		E ? e.button === 0 && (e.target?.releasePointerCapture?.(e.pointerId), !v && e.target === D.current && _.getState().userSelectionRect && te?.(e), _.setState({
			userSelectionActive: !1,
			userSelectionRect: null
		}), M.current && (c?.(e), _.setState({ nodesSelectionActive: k.current.size > 0 })), R()) : e.target === D.current && _.getState().connection.inProgress && (j.current = !0);
	}, ae = (e) => {
		e.target?.releasePointerCapture?.(e.pointerId), R();
	}, oe = r === !0 || Array.isArray(r) && r.includes(0);
	return (0, x.jsxs)("div", {
		className: S(["react-flow__pane", {
			draggable: oe,
			dragging: C,
			selection: e
		}]),
		onClick: E ? void 0 : K(te, D),
		onContextMenu: K(P, D),
		onWheel: K(F, D),
		onPointerEnter: E ? void 0 : f,
		onPointerMove: E ? z : p,
		onPointerUp: ie,
		onPointerCancel: E ? ae : void 0,
		onPointerDownCapture: E ? re : void 0,
		onClickCapture: E ? ne : void 0,
		onPointerLeave: m,
		ref: D,
		style: Cl,
		children: [h, (0, x.jsx)(Dl, {})]
	});
}
function Y({ id: e, store: t, unselect: n = !1, nodeRef: r }) {
	let { addSelectedNodes: i, unselectNodesAndEdges: a, multiSelectionActive: o, nodeLookup: s, onError: c } = t.getState(), l = s.get(e);
	l ? (t.setState({ nodesSelectionActive: !1 }), l.selected ? (n || l.selected && o) && (a({
		nodes: [l],
		edges: []
	}), requestAnimationFrame(() => r?.current?.blur())) : i([e])) : c?.("012", ya.error012(e));
}
function X({ nodeRef: e, disabled: t = !1, noDragClassName: n, handleSelector: r, nodeId: i, isSelectable: a, nodeClickDistance: o }) {
	let s = G(), [c, l] = (0, y.useState)(!1), u = (0, y.useRef)();
	return (0, y.useEffect)(() => {
		if (!t) return u.current = xs({
			getStoreItems: () => s.getState(),
			onNodeMouseDown: (t) => {
				Y({
					id: t,
					store: s,
					nodeRef: e
				});
			},
			onDragStart: () => {
				l(!0);
			},
			onDragStop: () => {
				l(!1);
			}
		}), () => {
			u.current?.destroy(), u.current = void 0;
		};
	}, [
		t,
		s,
		e
	]), (0, y.useEffect)(() => {
		!t && e.current && u.current && u.current.update({
			noDragClassName: n,
			handleSelector: r,
			domNode: e.current,
			isSelectable: a,
			nodeId: i,
			nodeClickDistance: o
		});
	}, [
		n,
		r,
		t,
		a,
		e,
		i,
		o
	]), c;
}
var Ol = (e) => (t) => t.selected && (t.draggable || e && t.draggable === void 0);
function kl() {
	let e = G();
	return (0, y.useCallback)((t) => {
		let { nodeExtent: n, snapToGrid: r, snapGrid: i, nodesDraggable: a, onError: o, updateNodePositions: s, nodeLookup: c, nodeOrigin: l } = e.getState(), u = /* @__PURE__ */ new Map(), d = Ol(a), f = r ? i[0] : 5, p = r ? i[1] : 5, m = t.direction.x * f * t.factor, h = t.direction.y * p * t.factor;
		for (let [, e] of c) {
			if (!d(e)) continue;
			let t = {
				x: e.internals.positionAbsolute.x + m,
				y: e.internals.positionAbsolute.y + h
			};
			r && (t = no(t, i));
			let { position: a, positionAbsolute: s } = Ba({
				nodeId: e.id,
				nextPosition: t,
				nodeLookup: c,
				nodeExtent: n,
				nodeOrigin: l,
				onError: o
			});
			e.position = a, e.internals.positionAbsolute = s, u.set(e.id, e);
		}
		s(u);
	}, []);
}
var Al = (0, y.createContext)(null), jl = Al.Provider;
Al.Consumer;
var Ml = () => (0, y.useContext)(Al), Nl = (e) => ({
	connectOnClick: e.connectOnClick,
	noPanClassName: e.noPanClassName,
	rfId: e.rfId
}), Pl = (0, y.createContext)(null);
function Fl({ children: e }) {
	let t = W(Nl, _c);
	return (0, x.jsx)(Pl.Provider, {
		value: t,
		children: e
	});
}
function Il() {
	let e = (0, y.useContext)(Pl);
	if (!e) throw Error("useHandleConfig must be used within a HandleConfigProvider");
	return e;
}
var Ll = {
	connectingFrom: !1,
	connectingTo: !1,
	clickConnecting: !1,
	isPossibleEndHandle: !0,
	connectionInProcess: !1,
	clickConnectionInProcess: !1,
	valid: !1
}, Rl = (e, t, n) => (r) => {
	let { connectionClickStartHandle: i, connectionMode: a, connection: o } = r, { fromHandle: s, toHandle: c, isValid: l } = o;
	if (!s && !i) return Ll;
	let u = c?.nodeId === e && c?.id === t && c?.type === n;
	return {
		connectingFrom: s?.nodeId === e && s?.id === t && s?.type === n,
		connectingTo: u,
		clickConnecting: i?.nodeId === e && i?.id === t && i?.type === n,
		isPossibleEndHandle: a === Ca.Strict ? s?.type !== n : e !== s?.nodeId || t !== s?.id,
		connectionInProcess: !!s,
		clickConnectionInProcess: !!i,
		valid: u && l
	};
};
function zl({ type: e = "source", position: t = V.Top, isValidConnection: n, isConnectable: r = !0, isConnectableStart: i = !0, isConnectableEnd: a = !0, id: o, onConnect: s, children: c, className: l, onMouseDown: u, onTouchStart: d, ...f }, p) {
	let m = o || null, h = e === "target", g = G(), _ = Ml(), { connectOnClick: v, noPanClassName: y, rfId: b } = Il(), { connectingFrom: C, connectingTo: w, clickConnecting: T, isPossibleEndHandle: E, connectionInProcess: D, clickConnectionInProcess: O, valid: k } = W(Rl(_, m, e), _c);
	_ || g.getState().onError?.("010", ya.error010());
	let A = (e) => {
		let { defaultEdgeOptions: t, onConnect: n, hasDefaultEdges: r } = g.getState(), i = {
			...t,
			...e
		};
		if (r) {
			let { edges: e, setEdges: t, onError: n } = g.getState();
			t(sl(i, e, { onError: n }));
		}
		n?.(i), s?.(i);
	}, j = (e) => {
		if (!_) return;
		let t = wo(e.nativeEvent);
		if (i && (t && e.button === 0 || !t)) {
			let t = g.getState();
			js.onPointerDown(e.nativeEvent, {
				handleDomNode: e.currentTarget,
				autoPanOnConnect: t.autoPanOnConnect,
				connectionMode: t.connectionMode,
				connectionRadius: t.connectionRadius,
				domNode: t.domNode,
				nodeLookup: t.nodeLookup,
				lib: t.lib,
				isTarget: h,
				handleId: m,
				nodeId: _,
				flowId: t.rfId,
				panBy: t.panBy,
				cancelConnection: t.cancelConnection,
				onConnectStart: t.onConnectStart,
				onConnectEnd: (...e) => g.getState().onConnectEnd?.(...e),
				updateConnection: t.updateConnection,
				onConnect: A,
				isValidConnection: n || ((...e) => g.getState().isValidConnection?.(...e) ?? !0),
				getTransform: () => g.getState().transform,
				getFromHandle: () => g.getState().connection.fromHandle,
				autoPanSpeed: t.autoPanSpeed,
				dragThreshold: t.connectionDragThreshold
			});
		}
		t ? u?.(e) : d?.(e);
	};
	return (0, x.jsx)("div", {
		"data-handleid": m,
		"data-nodeid": _,
		"data-handlepos": t,
		"data-id": `${b}-${_}-${m}-${e}`,
		className: S([
			"react-flow__handle",
			`react-flow__handle-${t}`,
			"nodrag",
			y,
			l,
			{
				source: !h,
				target: h,
				connectable: r,
				connectablestart: i,
				connectableend: a,
				clickconnecting: T,
				connectingfrom: C,
				connectingto: w,
				valid: k,
				connectionindicator: r && (!D || E) && (D || O ? a : i)
			}
		]),
		onMouseDown: j,
		onTouchStart: j,
		onClick: v ? (t) => {
			let { onClickConnectStart: r, onClickConnectEnd: a, connectionClickStartHandle: o, connectionMode: s, isValidConnection: c, lib: l, rfId: u, nodeLookup: d, connection: f } = g.getState();
			if (!_ || !o && !i) return;
			if (!o) {
				r?.(t.nativeEvent, {
					nodeId: _,
					handleId: m,
					handleType: e
				}), g.setState({ connectionClickStartHandle: {
					nodeId: _,
					type: e,
					id: m
				} });
				return;
			}
			let p = xo(t.target), h = n || c, { connection: v, isValid: y } = js.isValid(t.nativeEvent, {
				handle: {
					nodeId: _,
					id: m,
					type: e
				},
				connectionMode: s,
				fromNodeId: o.nodeId,
				fromHandleId: o.id || null,
				fromType: o.type,
				isValidConnection: h,
				flowId: u,
				doc: p,
				lib: l,
				nodeLookup: d
			});
			y && v && A(v);
			let b = structuredClone(f);
			delete b.inProgress, b.toPosition = b.toHandle ? b.toHandle.position : null, a?.(t, b), g.setState({ connectionClickStartHandle: null });
		} : void 0,
		ref: p,
		...f,
		children: c
	});
}
var Bl = (0, y.memo)(ul(zl));
function Vl({ data: e, isConnectable: t, sourcePosition: n = V.Bottom }) {
	return (0, x.jsxs)(x.Fragment, { children: [e?.label, (0, x.jsx)(Bl, {
		type: "source",
		position: n,
		isConnectable: t
	})] });
}
function Hl({ data: e, isConnectable: t, targetPosition: n = V.Top, sourcePosition: r = V.Bottom }) {
	return (0, x.jsxs)(x.Fragment, { children: [
		(0, x.jsx)(Bl, {
			type: "target",
			position: n,
			isConnectable: t
		}),
		e?.label,
		(0, x.jsx)(Bl, {
			type: "source",
			position: r,
			isConnectable: t
		})
	] });
}
function Ul() {
	return null;
}
function Wl({ data: e, isConnectable: t, targetPosition: n = V.Top }) {
	return (0, x.jsxs)(x.Fragment, { children: [(0, x.jsx)(Bl, {
		type: "target",
		position: n,
		isConnectable: t
	}), e?.label] });
}
var Gl = {
	ArrowUp: {
		x: 0,
		y: -1
	},
	ArrowDown: {
		x: 0,
		y: 1
	},
	ArrowLeft: {
		x: -1,
		y: 0
	},
	ArrowRight: {
		x: 1,
		y: 0
	}
}, Kl = {
	input: Vl,
	default: Hl,
	output: Wl,
	group: Ul
};
function ql(e) {
	return e.internals.handleBounds === void 0 ? {
		width: e.width ?? e.initialWidth ?? e.style?.width,
		height: e.height ?? e.initialHeight ?? e.style?.height
	} : {
		width: e.width ?? e.style?.width,
		height: e.height ?? e.style?.height
	};
}
var Jl = (e) => {
	let { width: t, height: n, x: r, y: i } = Fa(e.nodeLookup, { filter: (e) => !!e.selected });
	return {
		width: eo(t) ? t : null,
		height: eo(n) ? n : null,
		userSelectionActive: e.userSelectionActive,
		transformString: `translate(${e.transform[0]}px,${e.transform[1]}px) scale(${e.transform[2]}) translate(${r}px,${i}px)`
	};
};
function Yl({ onSelectionContextMenu: e, noPanClassName: t, disableKeyboardA11y: n }) {
	let r = G(), { width: i, height: a, transformString: o, userSelectionActive: s } = W(Jl, _c), c = kl(), l = (0, y.useRef)(null);
	(0, y.useEffect)(() => {
		n || l.current?.focus({ preventScroll: !0 });
	}, [n]);
	let u = !s && i !== null && a !== null;
	if (X({
		nodeRef: l,
		disabled: !u
	}), !u) return null;
	let d = e ? (t) => {
		e(t, r.getState().nodes.filter((e) => e.selected));
	} : void 0;
	return (0, x.jsx)("div", {
		className: S([
			"react-flow__nodesselection",
			"react-flow__container",
			t
		]),
		style: { transform: o },
		children: (0, x.jsx)("div", {
			ref: l,
			className: "react-flow__nodesselection-rect",
			onContextMenu: d,
			tabIndex: n ? void 0 : -1,
			onKeyDown: n ? void 0 : (e) => {
				Object.prototype.hasOwnProperty.call(Gl, e.key) && (e.preventDefault(), c({
					direction: Gl[e.key],
					factor: e.shiftKey ? 4 : 1
				}));
			},
			style: {
				width: i,
				height: a
			}
		})
	});
}
var Xl = typeof window < "u" ? window : void 0, Zl = (e) => ({
	nodesSelectionActive: e.nodesSelectionActive,
	userSelectionActive: e.userSelectionActive
});
function Ql({ children: e, onPaneClick: t, onPaneMouseEnter: n, onPaneMouseMove: r, onPaneMouseLeave: i, onPaneContextMenu: a, onPaneScroll: o, paneClickDistance: s, deleteKeyCode: c, selectionKeyCode: l, selectionOnDrag: u, selectionMode: d, onSelectionStart: f, onSelectionEnd: p, multiSelectionKeyCode: m, panActivationKeyCode: h, zoomActivationKeyCode: g, elementsSelectable: _, zoomOnScroll: v, zoomOnPinch: y, panOnScroll: b, panOnScrollSpeed: S, panOnScrollMode: C, zoomOnDoubleClick: w, panOnDrag: T, autoPanOnSelection: E, defaultViewport: D, translateExtent: O, minZoom: k, maxZoom: A, preventScrolling: j, onSelectionContextMenu: M, noWheelClassName: ee, noPanClassName: N, disableKeyboardA11y: te, onViewportChange: P, isControlledViewport: F }) {
	let { nodesSelectionActive: ne, userSelectionActive: re } = W(Zl, _c), I = Jc(l, { target: Xl }), L = Jc(h, { target: Xl }), R = L || T, z = L || b, ie = u && R !== !0, ae = I || re || ie;
	return xl({
		deleteKeyCode: c,
		multiSelectionKeyCode: m
	}), (0, x.jsx)(Tl, {
		onPaneContextMenu: a,
		elementsSelectable: _,
		zoomOnScroll: v,
		zoomOnPinch: y,
		panOnScroll: z,
		panActivationKeyPressed: L,
		panOnScrollSpeed: S,
		panOnScrollMode: C,
		zoomOnDoubleClick: w,
		panOnDrag: !I && R,
		defaultViewport: D,
		translateExtent: O,
		minZoom: k,
		maxZoom: A,
		zoomActivationKeyCode: g,
		preventScrolling: j,
		noWheelClassName: ee,
		noPanClassName: N,
		onViewportChange: P,
		isControlledViewport: F,
		paneClickDistance: s,
		selectionOnDrag: ie,
		children: (0, x.jsxs)(J, {
			onSelectionStart: f,
			onSelectionEnd: p,
			onPaneClick: t,
			onPaneMouseEnter: n,
			onPaneMouseMove: r,
			onPaneMouseLeave: i,
			onPaneContextMenu: a,
			onPaneScroll: o,
			panOnDrag: R,
			autoPanOnSelection: E,
			isSelecting: !!ae,
			selectionMode: d,
			selectionKeyPressed: I,
			paneClickDistance: s,
			selectionOnDrag: ie,
			children: [e, ne && (0, x.jsx)(Yl, {
				onSelectionContextMenu: M,
				noPanClassName: N,
				disableKeyboardA11y: te
			})]
		})
	});
}
Ql.displayName = "FlowRenderer";
var $l = (0, y.memo)(Ql), eu = (e) => (t) => e ? Ia(t.nodeLookup, {
	x: 0,
	y: 0,
	width: t.width,
	height: t.height
}, t.transform, !0).map((e) => e.id) : Array.from(t.nodeLookup.keys());
function tu(e) {
	return W((0, y.useCallback)(eu(e), [e]), _c);
}
var nu = (e) => e.updateNodeInternals;
function ru() {
	let e = W(nu), [t] = (0, y.useState)(() => typeof ResizeObserver > "u" ? null : new ResizeObserver((t) => {
		let n = /* @__PURE__ */ new Map();
		t.forEach((e) => {
			let t = e.target.getAttribute("data-id");
			n.set(t, {
				id: t,
				nodeElement: e.target,
				force: !0
			});
		}), e(n);
	}));
	return (0, y.useEffect)(() => () => {
		t?.disconnect();
	}, [t]), t;
}
function iu({ node: e, nodeType: t, hasDimensions: n, resizeObserver: r }) {
	let i = G(), a = (0, y.useRef)(null), o = (0, y.useRef)(null), s = (0, y.useRef)(e.sourcePosition), c = (0, y.useRef)(e.targetPosition), l = (0, y.useRef)(t), u = n && !!e.internals.handleBounds;
	return (0, y.useEffect)(() => {
		!a.current || e.hidden || u && o.current === a.current || (o.current && r?.unobserve(o.current), r?.observe(a.current), o.current = a.current);
	}, [u, e.hidden]), (0, y.useEffect)(() => () => {
		o.current &&= (r?.unobserve(o.current), null);
	}, []), (0, y.useEffect)(() => {
		if (a.current) {
			let n = l.current !== t, r = s.current !== e.sourcePosition, o = c.current !== e.targetPosition;
			(n || r || o) && (l.current = t, s.current = e.sourcePosition, c.current = e.targetPosition, i.getState().updateNodeInternals(/* @__PURE__ */ new Map([[e.id, {
				id: e.id,
				nodeElement: a.current,
				force: !0
			}]])));
		}
	}, [
		e.id,
		t,
		e.sourcePosition,
		e.targetPosition
	]), a;
}
function au({ id: e, onClick: t, onMouseEnter: n, onMouseMove: r, onMouseLeave: i, onContextMenu: a, onDoubleClick: o, nodesDraggable: s, elementsSelectable: c, nodesConnectable: l, nodesFocusable: u, resizeObserver: d, noDragClassName: f, noPanClassName: p, disableKeyboardA11y: m, rfId: h, nodeTypes: g, nodeClickDistance: _, onError: v }) {
	let { node: y, internals: b, isParent: C } = W((t) => {
		let n = t.nodeLookup.get(e), r = t.parentLookup.has(e);
		return {
			node: n,
			internals: n.internals,
			isParent: r
		};
	}, _c), w = y.type || "default", T = g?.[w] || Kl[w];
	T === void 0 && (v?.("003", ya.error003(w)), w = "default", T = g?.default || Kl.default);
	let E = !!(y.draggable || s && y.draggable === void 0), D = !!(y.selectable || c && y.selectable === void 0), O = !!(y.connectable || l && y.connectable === void 0), k = !!(y.focusable || u && y.focusable === void 0), A = G(), j = po(y), M = iu({
		node: y,
		nodeType: w,
		hasDimensions: j,
		resizeObserver: d
	}), ee = X({
		nodeRef: M,
		disabled: y.hidden || !E,
		noDragClassName: f,
		handleSelector: y.dragHandle,
		nodeId: e,
		isSelectable: D,
		nodeClickDistance: _
	}), N = kl();
	if (y.hidden) return null;
	let te = fo(y), P = ql(y), F = D || E || t || n || r || i, ne = n ? (e) => n(e, { ...b.userNode }) : void 0, re = r ? (e) => r(e, { ...b.userNode }) : void 0, I = i ? (e) => i(e, { ...b.userNode }) : void 0, L = a ? (e) => a(e, { ...b.userNode }) : void 0, R = o ? (e) => o(e, { ...b.userNode }) : void 0, z = (n) => {
		let { selectNodesOnDrag: r, nodeDragThreshold: i } = A.getState();
		D && (!r || !E || i > 0) && Y({
			id: e,
			store: A,
			nodeRef: M
		}), t && t(n, { ...b.userNode });
	}, ie = (t) => {
		if (!(Co(t.nativeEvent) || m)) {
			if (xa.includes(t.key) && D) {
				let n = t.key === "Escape";
				Y({
					id: e,
					store: A,
					unselect: n,
					nodeRef: M
				});
			} else if (E && y.selected && Object.prototype.hasOwnProperty.call(Gl, t.key)) {
				t.preventDefault();
				let { ariaLabelConfig: e } = A.getState();
				A.setState({ ariaLiveMessage: e["node.a11yDescription.ariaLiveMessage"]({
					direction: t.key.replace("Arrow", "").toLowerCase(),
					x: ~~b.positionAbsolute.x,
					y: ~~b.positionAbsolute.y
				}) }), N({
					direction: Gl[t.key],
					factor: t.shiftKey ? 4 : 1
				});
			}
		}
	}, ae = () => {
		if (m || !M.current?.matches(":focus-visible")) return;
		let { transform: t, width: n, height: r, autoPanOnNodeFocus: i, setCenter: a } = A.getState();
		i && (Ia(/* @__PURE__ */ new Map([[e, y]]), {
			x: 0,
			y: 0,
			width: n,
			height: r
		}, t, !0).length > 0 || a(y.position.x + te.width / 2, y.position.y + te.height / 2, { zoom: t[2] }));
	};
	return (0, x.jsx)("div", {
		className: S([
			"react-flow__node",
			`react-flow__node-${w}`,
			{ [p]: E },
			y.className,
			{
				selected: y.selected,
				selectable: D,
				parent: C,
				draggable: E,
				dragging: ee
			}
		]),
		ref: M,
		style: {
			zIndex: b.z,
			transform: `translate(${b.positionAbsolute.x}px,${b.positionAbsolute.y}px)`,
			pointerEvents: F ? "all" : "none",
			visibility: j ? "visible" : "hidden",
			...y.style,
			...P
		},
		"data-id": e,
		"data-testid": `rf__node-${e}`,
		onMouseEnter: ne,
		onMouseMove: re,
		onMouseLeave: I,
		onContextMenu: L,
		onClick: z,
		onDoubleClick: R,
		onKeyDown: k ? ie : void 0,
		tabIndex: k ? 0 : void 0,
		onFocus: k ? ae : void 0,
		role: y.ariaRole ?? (k ? "group" : void 0),
		"aria-roledescription": "node",
		"aria-describedby": m ? void 0 : `${Cc}-${h}`,
		"aria-label": y.ariaLabel,
		...y.domAttributes,
		children: (0, x.jsx)(jl, {
			value: e,
			children: (0, x.jsx)(T, {
				id: e,
				data: y.data,
				type: w,
				positionAbsoluteX: b.positionAbsolute.x,
				positionAbsoluteY: b.positionAbsolute.y,
				selected: y.selected ?? !1,
				selectable: D,
				draggable: E,
				deletable: y.deletable ?? !0,
				isConnectable: O,
				sourcePosition: y.sourcePosition,
				targetPosition: y.targetPosition,
				dragging: ee,
				dragHandle: y.dragHandle,
				zIndex: b.z,
				parentId: y.parentId,
				...te
			})
		})
	});
}
var ou = (0, y.memo)(au), su = (e) => ({
	nodesConnectable: e.nodesConnectable,
	nodesFocusable: e.nodesFocusable,
	elementsSelectable: e.elementsSelectable,
	onError: e.onError
});
function cu(e) {
	let { nodesConnectable: t, nodesFocusable: n, elementsSelectable: r, onError: i } = W(su, _c), a = tu(e.onlyRenderVisibleElements), o = ru();
	return (0, x.jsx)("div", {
		className: "react-flow__nodes",
		style: Cl,
		children: a.map((a) => (0, x.jsx)(ou, {
			id: a,
			nodeTypes: e.nodeTypes,
			nodeExtent: e.nodeExtent,
			onClick: e.onNodeClick,
			onMouseEnter: e.onNodeMouseEnter,
			onMouseMove: e.onNodeMouseMove,
			onMouseLeave: e.onNodeMouseLeave,
			onContextMenu: e.onNodeContextMenu,
			onDoubleClick: e.onNodeDoubleClick,
			noDragClassName: e.noDragClassName,
			noPanClassName: e.noPanClassName,
			rfId: e.rfId,
			disableKeyboardA11y: e.disableKeyboardA11y,
			resizeObserver: o,
			nodesDraggable: e.nodesDraggable ?? !0,
			nodesConnectable: t,
			nodesFocusable: n,
			elementsSelectable: r,
			nodeClickDistance: e.nodeClickDistance,
			onError: i
		}, a))
	});
}
cu.displayName = "NodeRenderer";
var lu = (0, y.memo)(cu);
function uu(e) {
	return W((0, y.useCallback)((t) => {
		if (!e) return t.edges.map((e) => e.id);
		let n = [];
		if (t.width && t.height) for (let e of t.edges) {
			let r = t.nodeLookup.get(e.source), i = t.nodeLookup.get(e.target);
			r && i && No({
				sourceNode: r,
				targetNode: i,
				width: t.width,
				height: t.height,
				transform: t.transform
			}) && n.push(e.id);
		}
		return n;
	}, [e]), _c);
}
var du = ({ color: e = "none", strokeWidth: t = 1 }) => {
	let n = {
		strokeWidth: t,
		...e && { stroke: e }
	};
	return (0, x.jsx)("polyline", {
		className: "arrow",
		style: n,
		strokeLinecap: "round",
		fill: "none",
		strokeLinejoin: "round",
		points: "-5,-4 0,0 -5,4"
	});
}, fu = ({ color: e = "none", strokeWidth: t = 1 }) => {
	let n = {
		strokeWidth: t,
		...e && {
			stroke: e,
			fill: e
		}
	};
	return (0, x.jsx)("polyline", {
		className: "arrowclosed",
		style: n,
		strokeLinecap: "round",
		strokeLinejoin: "round",
		points: "-5,-4 0,0 -5,4 -5,-4"
	});
}, pu = {
	[Oa.Arrow]: du,
	[Oa.ArrowClosed]: fu
};
function mu(e) {
	let t = G();
	return (0, y.useMemo)(() => Object.prototype.hasOwnProperty.call(pu, e) ? pu[e] : (t.getState().onError?.("009", ya.error009(e)), null), [e]);
}
var hu = ({ id: e, type: t, color: n, width: r = 12.5, height: i = 12.5, markerUnits: a = "strokeWidth", strokeWidth: o, orient: s = "auto-start-reverse" }) => {
	let c = mu(t);
	return c ? (0, x.jsx)("marker", {
		className: "react-flow__arrowhead",
		id: e,
		markerWidth: `${r}`,
		markerHeight: `${i}`,
		viewBox: "-10 -10 20 20",
		markerUnits: a,
		orient: s,
		refX: "0",
		refY: "0",
		children: (0, x.jsx)(c, {
			color: n,
			strokeWidth: o
		})
	}) : null;
}, gu = ({ defaultColor: e, rfId: t }) => {
	let n = W((e) => e.edges), r = W((e) => e.defaultEdgeOptions), i = (0, y.useMemo)(() => Zo(n, {
		id: t,
		defaultColor: e,
		defaultMarkerStart: r?.markerStart,
		defaultMarkerEnd: r?.markerEnd
	}), [
		n,
		r,
		t,
		e
	]);
	return i.length ? (0, x.jsx)("svg", {
		className: "react-flow__marker",
		"aria-hidden": "true",
		children: (0, x.jsx)("defs", { children: i.map((e) => (0, x.jsx)(hu, {
			id: e.id,
			type: e.type,
			color: e.color,
			width: e.width,
			height: e.height,
			markerUnits: e.markerUnits,
			strokeWidth: e.strokeWidth,
			orient: e.orient
		}, e.id)) })
	}) : null;
};
gu.displayName = "MarkerDefinitions";
var _u = (0, y.memo)(gu);
function vu({ x: e, y: t, label: n, labelStyle: r, labelShowBg: i = !0, labelBgStyle: a, labelBgPadding: o = [2, 4], labelBgBorderRadius: s = 2, children: c, className: l, ...u }) {
	let [d, f] = (0, y.useState)({
		x: 1,
		y: 0,
		width: 0,
		height: 0
	}), p = S(["react-flow__edge-textwrapper", l]), m = (0, y.useRef)(null);
	return (0, y.useEffect)(() => {
		if (m.current) {
			let e = m.current.getBBox();
			f({
				x: e.x,
				y: e.y,
				width: e.width,
				height: e.height
			});
		}
	}, [n]), n ? (0, x.jsxs)("g", {
		transform: `translate(${e - d.width / 2} ${t - d.height / 2})`,
		className: p,
		visibility: d.width ? "visible" : "hidden",
		...u,
		children: [
			i && (0, x.jsx)("rect", {
				width: d.width + 2 * o[0],
				x: -o[0],
				y: -o[1],
				height: d.height + 2 * o[1],
				className: "react-flow__edge-textbg",
				style: a,
				rx: s,
				ry: s
			}),
			(0, x.jsx)("text", {
				className: "react-flow__edge-text",
				y: d.height / 2,
				dy: "0.3em",
				ref: m,
				style: r,
				children: n
			}),
			c
		]
	}) : null;
}
vu.displayName = "EdgeText";
var yu = (0, y.memo)(vu);
function bu({ path: e, labelX: t, labelY: n, label: r, labelStyle: i, labelShowBg: a, labelBgStyle: o, labelBgPadding: s, labelBgBorderRadius: c, interactionWidth: l = 20, ...u }) {
	return (0, x.jsxs)(x.Fragment, { children: [
		(0, x.jsx)("path", {
			...u,
			d: e,
			fill: "none",
			className: S(["react-flow__edge-path", u.className])
		}),
		l ? (0, x.jsx)("path", {
			d: e,
			fill: "none",
			strokeOpacity: 0,
			strokeWidth: l,
			className: "react-flow__edge-interaction"
		}) : null,
		r && eo(t) && eo(n) ? (0, x.jsx)(yu, {
			x: t,
			y: n,
			label: r,
			labelStyle: i,
			labelShowBg: a,
			labelBgStyle: o,
			labelBgPadding: s,
			labelBgBorderRadius: c
		}) : null
	] });
}
function xu({ pos: e, x1: t, y1: n, x2: r, y2: i }) {
	return e === V.Left || e === V.Right ? [.5 * (t + r), n] : [t, .5 * (n + i)];
}
function Su({ sourceX: e, sourceY: t, sourcePosition: n = V.Bottom, targetX: r, targetY: i, targetPosition: a = V.Top }) {
	let [o, s] = xu({
		pos: n,
		x1: e,
		y1: t,
		x2: r,
		y2: i
	}), [c, l] = xu({
		pos: a,
		x1: r,
		y1: i,
		x2: e,
		y2: t
	}), [u, d, f, p] = Do({
		sourceX: e,
		sourceY: t,
		targetX: r,
		targetY: i,
		sourceControlX: o,
		sourceControlY: s,
		targetControlX: c,
		targetControlY: l
	});
	return [
		`M${e},${t} C${o},${s} ${c},${l} ${r},${i}`,
		u,
		d,
		f,
		p
	];
}
function Cu(e) {
	return (0, y.memo)(({ id: t, sourceX: n, sourceY: r, targetX: i, targetY: a, sourcePosition: o, targetPosition: s, label: c, labelStyle: l, labelShowBg: u, labelBgStyle: d, labelBgPadding: f, labelBgBorderRadius: p, style: m, markerEnd: h, markerStart: g, interactionWidth: _ }) => {
		let [v, y, b] = Su({
			sourceX: n,
			sourceY: r,
			sourcePosition: o,
			targetX: i,
			targetY: a,
			targetPosition: s
		}), S = e.isInternal ? void 0 : t;
		return (0, x.jsx)(bu, {
			id: S,
			path: v,
			labelX: y,
			labelY: b,
			label: c,
			labelStyle: l,
			labelShowBg: u,
			labelBgStyle: d,
			labelBgPadding: f,
			labelBgBorderRadius: p,
			style: m,
			markerEnd: h,
			markerStart: g,
			interactionWidth: _
		});
	});
}
var wu = Cu({ isInternal: !1 }), Tu = Cu({ isInternal: !0 });
wu.displayName = "SimpleBezierEdge", Tu.displayName = "SimpleBezierEdgeInternal";
function Eu(e) {
	return (0, y.memo)(({ id: t, sourceX: n, sourceY: r, targetX: i, targetY: a, label: o, labelStyle: s, labelShowBg: c, labelBgStyle: l, labelBgPadding: u, labelBgBorderRadius: d, style: f, sourcePosition: p = V.Bottom, targetPosition: m = V.Top, markerEnd: h, markerStart: g, pathOptions: _, interactionWidth: v }) => {
		let [y, b, S] = Wo({
			sourceX: n,
			sourceY: r,
			sourcePosition: p,
			targetX: i,
			targetY: a,
			targetPosition: m,
			borderRadius: _?.borderRadius,
			offset: _?.offset,
			stepPosition: _?.stepPosition
		}), C = e.isInternal ? void 0 : t;
		return (0, x.jsx)(bu, {
			id: C,
			path: y,
			labelX: b,
			labelY: S,
			label: o,
			labelStyle: s,
			labelShowBg: c,
			labelBgStyle: l,
			labelBgPadding: u,
			labelBgBorderRadius: d,
			style: f,
			markerEnd: h,
			markerStart: g,
			interactionWidth: v
		});
	});
}
var Du = Eu({ isInternal: !1 }), Ou = Eu({ isInternal: !0 });
Du.displayName = "SmoothStepEdge", Ou.displayName = "SmoothStepEdgeInternal";
function ku(e) {
	return (0, y.memo)(({ id: t, ...n }) => {
		let r = e.isInternal ? void 0 : t;
		return (0, x.jsx)(Du, {
			...n,
			id: r,
			pathOptions: (0, y.useMemo)(() => ({
				borderRadius: 0,
				offset: n.pathOptions?.offset
			}), [n.pathOptions?.offset])
		});
	});
}
var Au = ku({ isInternal: !1 }), ju = ku({ isInternal: !0 });
Au.displayName = "StepEdge", ju.displayName = "StepEdgeInternal";
function Z(e) {
	return (0, y.memo)(({ id: t, sourceX: n, sourceY: r, targetX: i, targetY: a, label: o, labelStyle: s, labelShowBg: c, labelBgStyle: l, labelBgPadding: u, labelBgBorderRadius: d, style: f, markerEnd: p, markerStart: m, interactionWidth: h }) => {
		let [g, _, v] = Lo({
			sourceX: n,
			sourceY: r,
			targetX: i,
			targetY: a
		}), y = e.isInternal ? void 0 : t;
		return (0, x.jsx)(bu, {
			id: y,
			path: g,
			labelX: _,
			labelY: v,
			label: o,
			labelStyle: s,
			labelShowBg: c,
			labelBgStyle: l,
			labelBgPadding: u,
			labelBgBorderRadius: d,
			style: f,
			markerEnd: p,
			markerStart: m,
			interactionWidth: h
		});
	});
}
var Mu = Z({ isInternal: !1 }), Nu = Z({ isInternal: !0 });
Mu.displayName = "StraightEdge", Nu.displayName = "StraightEdgeInternal";
function Pu(e) {
	return (0, y.memo)(({ id: t, sourceX: n, sourceY: r, targetX: i, targetY: a, sourcePosition: o = V.Bottom, targetPosition: s = V.Top, label: c, labelStyle: l, labelShowBg: u, labelBgStyle: d, labelBgPadding: f, labelBgBorderRadius: p, style: m, markerEnd: h, markerStart: g, pathOptions: _, interactionWidth: v }) => {
		let [y, b, S] = Ao({
			sourceX: n,
			sourceY: r,
			sourcePosition: o,
			targetX: i,
			targetY: a,
			targetPosition: s,
			curvature: _?.curvature
		}), C = e.isInternal ? void 0 : t;
		return (0, x.jsx)(bu, {
			id: C,
			path: y,
			labelX: b,
			labelY: S,
			label: c,
			labelStyle: l,
			labelShowBg: u,
			labelBgStyle: d,
			labelBgPadding: f,
			labelBgBorderRadius: p,
			style: m,
			markerEnd: h,
			markerStart: g,
			interactionWidth: v
		});
	});
}
var Fu = Pu({ isInternal: !1 }), Iu = Pu({ isInternal: !0 });
Fu.displayName = "BezierEdge", Iu.displayName = "BezierEdgeInternal";
var Lu = {
	default: Iu,
	straight: Nu,
	step: ju,
	smoothstep: Ou,
	simplebezier: Tu
}, Ru = {
	sourceX: null,
	sourceY: null,
	targetX: null,
	targetY: null,
	sourcePosition: null,
	targetPosition: null,
	zIndex: void 0
}, zu = (e, t, n) => n === V.Left ? e - t : n === V.Right ? e + t : e, Bu = (e, t, n) => n === V.Top ? e - t : n === V.Bottom ? e + t : e, Vu = "react-flow__edgeupdater";
function Hu({ position: e, centerX: t, centerY: n, radius: r = 10, onMouseDown: i, onMouseEnter: a, onMouseOut: o, type: s }) {
	return (0, x.jsx)("circle", {
		onMouseDown: i,
		onMouseEnter: a,
		onMouseOut: o,
		className: S([Vu, `${Vu}-${s}`]),
		cx: zu(t, r, e),
		cy: Bu(n, r, e),
		r,
		stroke: "transparent",
		fill: "transparent"
	});
}
function Uu({ isReconnectable: e, reconnectRadius: t, edge: n, sourceX: r, sourceY: i, targetX: a, targetY: o, sourcePosition: s, targetPosition: c, onReconnect: l, onReconnectStart: u, onReconnectEnd: d, setReconnecting: f, setUpdateHover: p }) {
	let m = G(), h = (e, t) => {
		if (e.button !== 0) return;
		let { autoPanOnConnect: r, domNode: i, connectionMode: a, connectionRadius: o, lib: s, onConnectStart: c, cancelConnection: p, nodeLookup: h, rfId: g, panBy: _, updateConnection: v } = m.getState(), y = t.type === "target";
		js.onPointerDown(e.nativeEvent, {
			autoPanOnConnect: r,
			connectionMode: a,
			connectionRadius: o,
			domNode: i,
			handleId: t.id,
			nodeId: t.nodeId,
			nodeLookup: h,
			isTarget: y,
			edgeUpdaterType: t.type,
			lib: s,
			flowId: g,
			cancelConnection: p,
			panBy: _,
			isValidConnection: (...e) => m.getState().isValidConnection?.(...e) ?? !0,
			onConnect: (e) => l?.(n, e),
			onConnectStart: (r, i) => {
				f(!0), u?.(e, n, t.type), c?.(r, i);
			},
			onConnectEnd: (...e) => m.getState().onConnectEnd?.(...e),
			onReconnectEnd: (e, r) => {
				f(!1), d?.(e, n, t.type, r);
			},
			updateConnection: v,
			getTransform: () => m.getState().transform,
			getFromHandle: () => m.getState().connection.fromHandle,
			dragThreshold: m.getState().connectionDragThreshold,
			handleDomNode: e.currentTarget
		});
	}, g = (e) => h(e, {
		nodeId: n.target,
		id: n.targetHandle ?? null,
		type: "target"
	}), _ = (e) => h(e, {
		nodeId: n.source,
		id: n.sourceHandle ?? null,
		type: "source"
	}), v = () => p(!0), y = () => p(!1);
	return (0, x.jsxs)(x.Fragment, { children: [(e === !0 || e === "source") && (0, x.jsx)(Hu, {
		position: s,
		centerX: r,
		centerY: i,
		radius: t,
		onMouseDown: g,
		onMouseEnter: v,
		onMouseOut: y,
		type: "source"
	}), (e === !0 || e === "target") && (0, x.jsx)(Hu, {
		position: c,
		centerX: a,
		centerY: o,
		radius: t,
		onMouseDown: _,
		onMouseEnter: v,
		onMouseOut: y,
		type: "target"
	})] });
}
function Wu({ id: e, edgesFocusable: t, edgesReconnectable: n, elementsSelectable: r, onClick: i, onDoubleClick: a, onContextMenu: o, onMouseEnter: s, onMouseMove: c, onMouseLeave: l, reconnectRadius: u, onReconnect: d, onReconnectStart: f, onReconnectEnd: p, rfId: m, edgeTypes: h, noPanClassName: g, onError: _, disableKeyboardA11y: v }) {
	let b = W((t) => t.edgeLookup.get(e)), C = W((e) => e.defaultEdgeOptions);
	b = C ? {
		...C,
		...b
	} : b;
	let w = b.type || "default", T = h?.[w] || Lu[w];
	T === void 0 && (_?.("011", ya.error011(w)), w = "default", T = h?.default || Lu.default);
	let E = !!(b.focusable || t && b.focusable === void 0), D = d !== void 0 && (b.reconnectable || n && b.reconnectable === void 0), O = !!(b.selectable || r && b.selectable === void 0), k = (0, y.useRef)(null), [A, j] = (0, y.useState)(!1), [M, ee] = (0, y.useState)(!1), N = G(), { zIndex: te = b.zIndex, sourceX: P, sourceY: F, targetX: ne, targetY: re, sourcePosition: I, targetPosition: L } = W((0, y.useCallback)((t) => {
		let n = t.nodeLookup.get(b.source), r = t.nodeLookup.get(b.target);
		if (!n || !r) return Ru;
		let i = Ko({
			id: e,
			sourceNode: n,
			targetNode: r,
			sourceHandle: b.sourceHandle || null,
			targetHandle: b.targetHandle || null,
			connectionMode: t.connectionMode,
			onError: _
		}), a = Mo({
			selected: b.selected,
			zIndex: b.zIndex,
			sourceNode: n,
			targetNode: r,
			elevateOnSelect: t.elevateEdgesOnSelect,
			zIndexMode: t.zIndexMode
		});
		return {
			...i || Ru,
			zIndex: a
		};
	}, [
		b.source,
		b.target,
		b.sourceHandle,
		b.targetHandle,
		b.selected,
		b.zIndex,
		_
	]), _c), R = (0, y.useMemo)(() => b.markerStart ? `url('#${Xo(b.markerStart, m)}')` : void 0, [b.markerStart, m]), z = (0, y.useMemo)(() => b.markerEnd ? `url('#${Xo(b.markerEnd, m)}')` : void 0, [b.markerEnd, m]);
	if (b.hidden || P === null || F === null || ne === null || re === null) return null;
	let ie = (t) => {
		let { addSelectedEdges: n, unselectNodesAndEdges: r, multiSelectionActive: a } = N.getState();
		O && (N.setState({ nodesSelectionActive: !1 }), b.selected && a ? (r({
			nodes: [],
			edges: [b]
		}), k.current?.blur()) : n([e])), i && i(t, b);
	}, ae = a ? (e) => {
		a(e, { ...b });
	} : void 0, oe = o ? (e) => {
		o(e, { ...b });
	} : void 0, se = s ? (e) => {
		s(e, { ...b });
	} : void 0, ce = c ? (e) => {
		c(e, { ...b });
	} : void 0, le = l ? (e) => {
		l(e, { ...b });
	} : void 0;
	return (0, x.jsx)("svg", {
		style: { zIndex: te },
		children: (0, x.jsxs)("g", {
			className: S([
				"react-flow__edge",
				`react-flow__edge-${w}`,
				b.className,
				g,
				{
					selected: b.selected,
					animated: b.animated,
					inactive: !O && !i,
					updating: A,
					selectable: O
				}
			]),
			onClick: ie,
			onDoubleClick: ae,
			onContextMenu: oe,
			onMouseEnter: se,
			onMouseMove: ce,
			onMouseLeave: le,
			onKeyDown: E ? (t) => {
				if (!v && xa.includes(t.key) && O) {
					let { unselectNodesAndEdges: n, addSelectedEdges: r } = N.getState();
					t.key === "Escape" ? (k.current?.blur(), n({ edges: [b] })) : r([e]);
				}
			} : void 0,
			tabIndex: E ? 0 : void 0,
			role: b.ariaRole ?? (E ? "group" : "img"),
			"aria-roledescription": "edge",
			"data-id": e,
			"data-testid": `rf__edge-${e}`,
			"aria-label": b.ariaLabel === null ? void 0 : b.ariaLabel || `Edge from ${b.source} to ${b.target}`,
			"aria-describedby": E ? `${wc}-${m}` : void 0,
			ref: k,
			...b.domAttributes,
			children: [!M && (0, x.jsx)(T, {
				id: e,
				source: b.source,
				target: b.target,
				type: b.type,
				selected: b.selected,
				animated: b.animated,
				selectable: O,
				deletable: b.deletable ?? !0,
				label: b.label,
				labelStyle: b.labelStyle,
				labelShowBg: b.labelShowBg,
				labelBgStyle: b.labelBgStyle,
				labelBgPadding: b.labelBgPadding,
				labelBgBorderRadius: b.labelBgBorderRadius,
				sourceX: P,
				sourceY: F,
				targetX: ne,
				targetY: re,
				sourcePosition: I,
				targetPosition: L,
				data: b.data,
				style: b.style,
				sourceHandleId: b.sourceHandle,
				targetHandleId: b.targetHandle,
				markerStart: R,
				markerEnd: z,
				pathOptions: "pathOptions" in b ? b.pathOptions : void 0,
				interactionWidth: b.interactionWidth
			}), D && (0, x.jsx)(Uu, {
				edge: b,
				isReconnectable: D,
				reconnectRadius: u,
				onReconnect: d,
				onReconnectStart: f,
				onReconnectEnd: p,
				sourceX: P,
				sourceY: F,
				targetX: ne,
				targetY: re,
				sourcePosition: I,
				targetPosition: L,
				setUpdateHover: j,
				setReconnecting: ee
			})]
		})
	});
}
var Gu = (0, y.memo)(Wu), Ku = (e) => ({
	edgesFocusable: e.edgesFocusable,
	edgesReconnectable: e.edgesReconnectable,
	elementsSelectable: e.elementsSelectable,
	connectionMode: e.connectionMode,
	onError: e.onError
});
function qu({ defaultMarkerColor: e, onlyRenderVisibleElements: t, rfId: n, edgeTypes: r, noPanClassName: i, onReconnect: a, onEdgeContextMenu: o, onEdgeMouseEnter: s, onEdgeMouseMove: c, onEdgeMouseLeave: l, onEdgeClick: u, reconnectRadius: d, onEdgeDoubleClick: f, onReconnectStart: p, onReconnectEnd: m, disableKeyboardA11y: h }) {
	let { edgesFocusable: g, edgesReconnectable: _, elementsSelectable: v, onError: y } = W(Ku, _c), b = uu(t);
	return (0, x.jsxs)("div", {
		className: "react-flow__edges",
		children: [(0, x.jsx)(_u, {
			defaultColor: e,
			rfId: n
		}), b.map((e) => (0, x.jsx)(Gu, {
			id: e,
			edgesFocusable: g,
			edgesReconnectable: _,
			elementsSelectable: v,
			noPanClassName: i,
			onReconnect: a,
			onContextMenu: o,
			onMouseEnter: s,
			onMouseMove: c,
			onMouseLeave: l,
			onClick: u,
			reconnectRadius: d,
			onDoubleClick: f,
			onReconnectStart: p,
			onReconnectEnd: m,
			rfId: n,
			onError: y,
			edgeTypes: r,
			disableKeyboardA11y: h
		}, e))]
	});
}
qu.displayName = "EdgeRenderer";
var Ju = (0, y.memo)(qu), Yu = (e) => `translate(${e[0]}px,${e[1]}px) scale(${e[2]})`;
function Xu({ children: e }) {
	let t = G(), n = (0, y.useRef)(null), [r] = (0, y.useState)(() => t.getState().transform);
	return dl(() => {
		let e = null, r = () => {
			let r = t.getState().transform;
			e && r[0] === e[0] && r[1] === e[1] && r[2] === e[2] || (e = r, n.current && (n.current.style.transform = Yu(r)));
		};
		return r(), t.subscribe(r);
	}, [t]), (0, x.jsx)("div", {
		ref: n,
		className: "react-flow__viewport xyflow__viewport react-flow__container",
		style: { transform: Yu(r) },
		children: e
	});
}
function Zu(e) {
	let t = vl(), n = (0, y.useRef)(!1);
	(0, y.useEffect)(() => {
		!n.current && t.viewportInitialized && e && (setTimeout(() => e(t), 1), n.current = !0);
	}, [e, t.viewportInitialized]);
}
var Qu = (e) => e.panZoom?.syncViewport;
function $u(e) {
	let t = W(Qu), n = G();
	return (0, y.useEffect)(() => {
		e && (t?.(e), n.setState({ transform: [
			e.x,
			e.y,
			e.zoom
		] }));
	}, [e, t]), null;
}
function ed(e) {
	return e.connection.inProgress ? {
		...e.connection,
		to: ro(e.connection.to, e.transform)
	} : { ...e.connection };
}
function td(e) {
	return e ? (t) => e(ed(t)) : ed;
}
function nd(e) {
	return W(td(e), _c);
}
var rd = (e) => ({
	nodesConnectable: e.nodesConnectable,
	isValid: e.connection.isValid,
	inProgress: e.connection.inProgress,
	width: e.width,
	height: e.height
});
function id({ containerStyle: e, style: t, type: n, component: r }) {
	let { nodesConnectable: i, width: a, height: o, isValid: s, inProgress: c } = W(rd, _c);
	return a && i && c ? (0, x.jsx)("svg", {
		style: e,
		width: a,
		height: o,
		className: "react-flow__connectionline react-flow__container",
		children: (0, x.jsx)("g", {
			className: S(["react-flow__connection", vo(s)]),
			children: (0, x.jsx)(ad, {
				style: t,
				type: n,
				CustomComponent: r,
				isValid: s
			})
		})
	}) : null;
}
var ad = ({ style: e, type: t = Da.Bezier, CustomComponent: n, isValid: r }) => {
	let { inProgress: i, from: a, fromNode: o, fromHandle: s, fromPosition: c, to: l, toNode: u, toHandle: d, toPosition: f, pointer: p } = nd();
	if (!i) return;
	if (n) return (0, x.jsx)(n, {
		connectionLineType: t,
		connectionLineStyle: e,
		fromNode: o,
		fromHandle: s,
		fromX: a.x,
		fromY: a.y,
		toX: l.x,
		toY: l.y,
		fromPosition: c,
		toPosition: f,
		connectionStatus: vo(r),
		toNode: u,
		toHandle: d,
		pointer: p
	});
	let m = "", h = {
		sourceX: a.x,
		sourceY: a.y,
		sourcePosition: c,
		targetX: l.x,
		targetY: l.y,
		targetPosition: f
	};
	switch (t) {
		case Da.Bezier:
			[m] = Ao(h);
			break;
		case Da.SimpleBezier:
			[m] = Su(h);
			break;
		case Da.Step:
			[m] = Wo({
				...h,
				borderRadius: 0
			});
			break;
		case Da.SmoothStep:
			[m] = Wo(h);
			break;
		default: [m] = Lo(h);
	}
	return (0, x.jsx)("path", {
		d: m,
		fill: "none",
		className: "react-flow__connection-path",
		style: e
	});
};
ad.displayName = "ConnectionLine";
var od = {};
function Q(e = od) {
	(0, y.useRef)(e), G(), (0, y.useEffect)(() => {}, [e]);
}
function sd() {
	G(), (0, y.useRef)(!1), (0, y.useEffect)(() => {}, []);
}
function cd({ nodeTypes: e, edgeTypes: t, onInit: n, onNodeClick: r, onEdgeClick: i, onNodeDoubleClick: a, onEdgeDoubleClick: o, onNodeMouseEnter: s, onNodeMouseMove: c, onNodeMouseLeave: l, onNodeContextMenu: u, onSelectionContextMenu: d, onSelectionStart: f, onSelectionEnd: p, connectionLineType: m, connectionLineStyle: h, connectionLineComponent: g, connectionLineContainerStyle: _, selectionKeyCode: v, selectionOnDrag: y, selectionMode: b, multiSelectionKeyCode: S, panActivationKeyCode: C, zoomActivationKeyCode: w, deleteKeyCode: T, onlyRenderVisibleElements: E, elementsSelectable: D, defaultViewport: O, translateExtent: k, minZoom: A, maxZoom: j, preventScrolling: M, defaultMarkerColor: ee, zoomOnScroll: N, zoomOnPinch: te, panOnScroll: P, panOnScrollSpeed: F, panOnScrollMode: ne, zoomOnDoubleClick: re, panOnDrag: I, autoPanOnSelection: L, onPaneClick: R, onPaneMouseEnter: z, onPaneMouseMove: ie, onPaneMouseLeave: ae, onPaneScroll: oe, onPaneContextMenu: se, paneClickDistance: ce, nodeClickDistance: le, onEdgeContextMenu: ue, onEdgeMouseEnter: de, onEdgeMouseMove: fe, onEdgeMouseLeave: pe, reconnectRadius: me, onReconnect: he, onReconnectStart: ge, onReconnectEnd: _e, noDragClassName: ve, noWheelClassName: ye, noPanClassName: be, disableKeyboardA11y: xe, nodeExtent: Se, rfId: Ce, viewport: we, onViewportChange: Te, nodesDraggable: Ee }) {
	return Q(e), Q(t), sd(), Zu(n), $u(we), (0, x.jsx)($l, {
		onPaneClick: R,
		onPaneMouseEnter: z,
		onPaneMouseMove: ie,
		onPaneMouseLeave: ae,
		onPaneContextMenu: se,
		onPaneScroll: oe,
		paneClickDistance: ce,
		deleteKeyCode: T,
		selectionKeyCode: v,
		selectionOnDrag: y,
		selectionMode: b,
		onSelectionStart: f,
		onSelectionEnd: p,
		multiSelectionKeyCode: S,
		panActivationKeyCode: C,
		zoomActivationKeyCode: w,
		elementsSelectable: D,
		zoomOnScroll: N,
		zoomOnPinch: te,
		zoomOnDoubleClick: re,
		panOnScroll: P,
		panOnScrollSpeed: F,
		panOnScrollMode: ne,
		panOnDrag: I,
		autoPanOnSelection: L,
		defaultViewport: O,
		translateExtent: k,
		minZoom: A,
		maxZoom: j,
		onSelectionContextMenu: d,
		preventScrolling: M,
		noDragClassName: ve,
		noWheelClassName: ye,
		noPanClassName: be,
		disableKeyboardA11y: xe,
		onViewportChange: Te,
		isControlledViewport: !!we,
		children: (0, x.jsxs)(Xu, { children: [
			(0, x.jsx)(Ju, {
				edgeTypes: t,
				onEdgeClick: i,
				onEdgeDoubleClick: o,
				onReconnect: he,
				onReconnectStart: ge,
				onReconnectEnd: _e,
				onlyRenderVisibleElements: E,
				onEdgeContextMenu: ue,
				onEdgeMouseEnter: de,
				onEdgeMouseMove: fe,
				onEdgeMouseLeave: pe,
				reconnectRadius: me,
				defaultMarkerColor: ee,
				noPanClassName: be,
				disableKeyboardA11y: xe,
				rfId: Ce
			}),
			(0, x.jsx)(id, {
				style: h,
				type: m,
				component: g,
				containerStyle: _
			}),
			(0, x.jsx)("div", { className: "react-flow__edgelabel-renderer" }),
			(0, x.jsx)(lu, {
				nodeTypes: e,
				onNodeClick: r,
				onNodeDoubleClick: a,
				onNodeMouseEnter: s,
				onNodeMouseMove: c,
				onNodeMouseLeave: l,
				onNodeContextMenu: u,
				nodeClickDistance: le,
				onlyRenderVisibleElements: E,
				noPanClassName: be,
				noDragClassName: ve,
				disableKeyboardA11y: xe,
				nodeExtent: Se,
				rfId: Ce,
				nodesDraggable: Ee
			}),
			(0, x.jsx)("div", { className: "react-flow__viewport-portal" })
		] })
	});
}
cd.displayName = "GraphView";
var ld = (0, y.memo)(cd), ud = to("React Flow", "https://reactflow.dev/"), dd = ({ nodes: e, edges: t, defaultNodes: n, defaultEdges: r, width: i, height: a, fitView: o, fitViewOptions: s, minZoom: c = .5, maxZoom: l = 2, nodeOrigin: u, nodeExtent: d, zIndexMode: f = "basic" } = {}) => {
	let p = /* @__PURE__ */ new Map(), m = /* @__PURE__ */ new Map(), h = /* @__PURE__ */ new Map(), g = /* @__PURE__ */ new Map(), _ = r ?? t ?? [], v = n ?? e ?? [], y = u ?? [0, 0], b = d ?? ba;
	hs(h, g, _);
	let { nodesInitialized: x } = os(v, p, m, {
		nodeOrigin: y,
		nodeExtent: b,
		zIndexMode: f
	}), S = [
		0,
		0,
		1
	];
	if (o && i && a) {
		let { x: e, y: t, zoom: n } = co(Fa(p, { filter: (e) => !(!e.width && !e.initialWidth || !e.height && !e.initialHeight) }), i, a, c, l, s?.padding ?? .1);
		S = [
			e,
			t,
			n
		];
	}
	return {
		rfId: "1",
		width: i ?? 0,
		height: a ?? 0,
		transform: S,
		nodes: v,
		nodesInitialized: x,
		nodeLookup: p,
		parentLookup: m,
		edges: _,
		edgeLookup: g,
		connectionLookup: h,
		onNodesChange: null,
		onEdgesChange: null,
		hasDefaultNodes: n !== void 0,
		hasDefaultEdges: r !== void 0,
		panZoom: null,
		minZoom: c,
		maxZoom: l,
		translateExtent: ba,
		nodeExtent: b,
		nodesSelectionActive: !1,
		userSelectionActive: !1,
		userSelectionRect: null,
		connectionMode: Ca.Strict,
		domNode: null,
		paneDragging: !1,
		noPanClassName: "nopan",
		nodeOrigin: y,
		nodeDragThreshold: 1,
		connectionDragThreshold: 1,
		snapGrid: [15, 15],
		snapToGrid: !1,
		nodesDraggable: !0,
		nodesConnectable: !0,
		nodesFocusable: !0,
		edgesFocusable: !0,
		edgesReconnectable: !0,
		elementsSelectable: !0,
		elevateNodesOnSelect: !0,
		elevateEdgesOnSelect: !0,
		selectNodesOnDrag: !0,
		multiSelectionActive: !1,
		fitViewQueued: o ?? !1,
		fitViewOptions: s,
		fitViewResolver: null,
		connection: { ...Ea },
		connectionClickStartHandle: null,
		connectOnClick: !0,
		ariaLiveMessage: "",
		autoPanOnConnect: !0,
		autoPanOnNodeDrag: !0,
		autoPanOnNodeFocus: !0,
		autoPanSpeed: 15,
		connectionRadius: 20,
		onError: ud,
		isValidConnection: void 0,
		onSelectionChangeHandlers: [],
		lib: "react",
		debug: !1,
		ariaLabelConfig: Sa,
		zIndexMode: f,
		defaultEdgeOptions: void 0,
		onNodesChangeMiddlewareMap: /* @__PURE__ */ new Map(),
		onEdgesChangeMiddlewareMap: /* @__PURE__ */ new Map(),
		onNodesDelete: void 0,
		onEdgesDelete: void 0,
		onDelete: void 0,
		onBeforeDelete: void 0,
		onViewportChangeStart: void 0,
		onViewportChange: void 0,
		onViewportChangeEnd: void 0,
		onNodeDragStart: void 0,
		onNodeDrag: void 0,
		onNodeDragStop: void 0,
		onSelectionDragStart: void 0,
		onSelectionDrag: void 0,
		onSelectionDragStop: void 0,
		onMoveStart: void 0,
		onMove: void 0,
		onMoveEnd: void 0,
		onConnect: void 0,
		onConnectStart: void 0,
		onConnectEnd: void 0,
		onClickConnectStart: void 0,
		onClickConnectEnd: void 0
	};
}, fd = ({ nodes: e, edges: t, defaultNodes: n, defaultEdges: r, width: i, height: a, fitView: o, fitViewOptions: s, minZoom: c, maxZoom: l, nodeOrigin: u, nodeExtent: d, zIndexMode: f }) => gc((p, m) => {
	async function h() {
		let { nodeLookup: e, panZoom: t, fitViewOptions: n, fitViewResolver: r, width: i, height: a, minZoom: o, maxZoom: s } = m();
		t && (await za({
			nodes: e,
			width: i,
			height: a,
			panZoom: t,
			minZoom: o,
			maxZoom: s
		}, n), r?.resolve(!0), p({ fitViewResolver: null }));
	}
	return {
		...dd({
			nodes: e,
			edges: t,
			width: i,
			height: a,
			fitView: o,
			fitViewOptions: s,
			minZoom: c,
			maxZoom: l,
			nodeOrigin: u,
			nodeExtent: d,
			defaultNodes: n,
			defaultEdges: r,
			zIndexMode: f
		}),
		setNodes: (e) => {
			let { nodeLookup: t, parentLookup: n, nodeOrigin: r, nodeExtent: i, elevateNodesOnSelect: a, fitViewQueued: o, zIndexMode: s, nodesSelectionActive: c } = m(), { nodesInitialized: l, hasSelectedNodes: u } = os(e, t, n, {
				nodeOrigin: r,
				nodeExtent: i,
				elevateNodesOnSelect: a,
				checkEquality: !0,
				zIndexMode: s
			}), d = c && u;
			o && l ? (h(), p({
				nodes: e,
				nodesInitialized: l,
				fitViewQueued: !1,
				fitViewOptions: void 0,
				nodesSelectionActive: d
			})) : p({
				nodes: e,
				nodesInitialized: l,
				nodesSelectionActive: d
			});
		},
		setEdges: (e) => {
			let { connectionLookup: t, edgeLookup: n } = m();
			hs(t, n, e), p({ edges: e });
		},
		setDefaultNodesAndEdges: (e, t) => {
			if (e) {
				let { setNodes: t } = m();
				t(e), p({ hasDefaultNodes: !0 });
			}
			if (t) {
				let { setEdges: e } = m();
				e(t), p({ hasDefaultEdges: !0 });
			}
		},
		updateNodeInternals: (e) => {
			let { triggerNodeChanges: t, nodeLookup: n, parentLookup: r, domNode: i, nodeOrigin: a, nodeExtent: o, debug: s, fitViewQueued: c, zIndexMode: l } = m(), { changes: u, updatedInternals: d } = fs(e, n, r, i, a, o, l);
			d && (rs(n, r, {
				nodeOrigin: a,
				nodeExtent: o,
				zIndexMode: l
			}), c ? (h(), p({
				fitViewQueued: !1,
				fitViewOptions: void 0
			})) : p({}), u?.length > 0 && (s && console.log("React Flow: trigger node changes", u), t?.(u)));
		},
		updateNodePositions: (e, t = !1) => {
			let n = [], r = [], { nodeLookup: i, triggerNodeChanges: a, connection: o, updateConnection: s, onNodesChangeMiddlewareMap: c } = m();
			for (let [a, c] of e) {
				let e = i.get(a), l = !!(e?.expandParent && e?.parentId && c?.position), u = {
					id: a,
					type: "position",
					position: l ? {
						x: Math.max(0, c.position.x),
						y: Math.max(0, c.position.y)
					} : c.position,
					dragging: t
				};
				if (e && o.inProgress && o.fromNode.id === e.id) {
					let t = Jo(e, o.fromHandle, V.Left, !0);
					s({
						...o,
						from: t
					});
				}
				l && e.parentId && n.push({
					id: a,
					parentId: e.parentId,
					rect: {
						...c.internals.positionAbsolute,
						width: c.measured.width ?? 0,
						height: c.measured.height ?? 0
					}
				}), r.push(u);
			}
			if (n.length > 0) {
				let { parentLookup: e, nodeOrigin: t } = m(), a = ds(n, i, e, t);
				r.push(...a);
			}
			for (let e of c.values()) r = e(r);
			a(r);
		},
		triggerNodeChanges: (e) => {
			let { onNodesChange: t, setNodes: n, nodes: r, hasDefaultNodes: i, debug: a } = m();
			e?.length && (i && n(el(e, r)), a && console.log("React Flow: trigger node changes", e), t?.(e));
		},
		triggerEdgeChanges: (e) => {
			let { onEdgesChange: t, setEdges: n, edges: r, hasDefaultEdges: i, debug: a } = m();
			e?.length && (i && n(tl(e, r)), a && console.log("React Flow: trigger edge changes", e), t?.(e));
		},
		addSelectedNodes: (e) => {
			let { multiSelectionActive: t, edgeLookup: n, nodeLookup: r, triggerNodeChanges: i, triggerEdgeChanges: a } = m();
			t ? i(e.map((e) => nl(e, !0))) : (i(rl(r, /* @__PURE__ */ new Set([...e]))), a(rl(n)));
		},
		addSelectedEdges: (e) => {
			let { multiSelectionActive: t, edgeLookup: n, nodeLookup: r, triggerNodeChanges: i, triggerEdgeChanges: a } = m();
			t ? a(e.map((e) => nl(e, !0))) : (a(rl(n, /* @__PURE__ */ new Set([...e]))), i(rl(r, /* @__PURE__ */ new Set())));
		},
		unselectNodesAndEdges: ({ nodes: e, edges: t } = {}) => {
			let { edges: n, nodes: r, nodeLookup: i, triggerNodeChanges: a, triggerEdgeChanges: o } = m(), s = e || r, c = t || n, l = [];
			for (let e of s) e.selected && l.push(nl(e.id, !1));
			let u = [];
			for (let e of c) e.selected && u.push(nl(e.id, !1));
			a(l), o(u);
		},
		setMinZoom: (e) => {
			let { panZoom: t, maxZoom: n } = m();
			t?.setScaleExtent([e, n]), p({ minZoom: e });
		},
		setMaxZoom: (e) => {
			let { panZoom: t, minZoom: n } = m();
			t?.setScaleExtent([n, e]), p({ maxZoom: e });
		},
		setTranslateExtent: (e) => {
			m().panZoom?.setTranslateExtent(e), p({ translateExtent: e });
		},
		resetSelectedElements: () => {
			let { edges: e, nodes: t, triggerNodeChanges: n, triggerEdgeChanges: r, elementsSelectable: i } = m();
			if (!i) return;
			let a = t.reduce((e, t) => t.selected ? [...e, nl(t.id, !1)] : e, []), o = e.reduce((e, t) => t.selected ? [...e, nl(t.id, !1)] : e, []);
			n(a), r(o);
		},
		setNodeExtent: (e) => {
			let { nodes: t, nodeLookup: n, parentLookup: r, nodeOrigin: i, elevateNodesOnSelect: a, nodeExtent: o, zIndexMode: s } = m();
			(e[0][0] !== o[0][0] || e[0][1] !== o[0][1] || e[1][0] !== o[1][0] || e[1][1] !== o[1][1]) && (os(t, n, r, {
				nodeOrigin: i,
				nodeExtent: e,
				elevateNodesOnSelect: a,
				checkEquality: !1,
				zIndexMode: s
			}), p({ nodeExtent: e }));
		},
		panBy: (e) => {
			let { transform: t, width: n, height: r, panZoom: i, translateExtent: a } = m();
			return ps({
				delta: e,
				panZoom: i,
				transform: t,
				translateExtent: a,
				width: n,
				height: r
			});
		},
		setCenter: async (e, t, n) => {
			let { width: r, height: i, maxZoom: a, panZoom: o } = m();
			if (!o) return !1;
			let s = n?.zoom === void 0 ? a : n.zoom;
			return await o.setViewport({
				x: r / 2 - e * s,
				y: i / 2 - t * s,
				zoom: s
			}, {
				duration: n?.duration,
				ease: n?.ease,
				interpolate: n?.interpolate
			}), !0;
		},
		cancelConnection: () => {
			p({ connection: { ...Ea } });
		},
		updateConnection: (e) => {
			p({ connection: e });
		},
		reset: () => p({ ...dd() })
	};
}, Object.is);
function pd({ initialNodes: e, initialEdges: t, defaultNodes: n, defaultEdges: r, initialWidth: i, initialHeight: a, initialMinZoom: o, initialMaxZoom: s, initialFitViewOptions: c, fitView: l, nodeOrigin: u, nodeExtent: d, zIndexMode: f, children: p }) {
	let [m] = (0, y.useState)(() => fd({
		nodes: e,
		edges: t,
		defaultNodes: n,
		defaultEdges: r,
		width: i,
		height: a,
		fitView: l,
		minZoom: o,
		maxZoom: s,
		fitViewOptions: c,
		nodeOrigin: u,
		nodeExtent: d,
		zIndexMode: f
	}));
	return (0, x.jsx)(yc, {
		value: m,
		children: (0, x.jsx)(hl, { children: (0, x.jsx)(Fl, { children: p }) })
	});
}
function md({ children: e, nodes: t, edges: n, defaultNodes: r, defaultEdges: i, width: a, height: o, fitView: s, fitViewOptions: c, minZoom: l, maxZoom: u, nodeOrigin: d, nodeExtent: f, zIndexMode: p }) {
	return (0, y.useContext)(vc) ? (0, x.jsx)(x.Fragment, { children: e }) : (0, x.jsx)(pd, {
		initialNodes: t,
		initialEdges: n,
		defaultNodes: r,
		defaultEdges: i,
		initialWidth: a,
		initialHeight: o,
		fitView: s,
		initialFitViewOptions: c,
		initialMinZoom: l,
		initialMaxZoom: u,
		nodeOrigin: d,
		nodeExtent: f,
		zIndexMode: p,
		children: e
	});
}
var hd = {
	width: "100%",
	height: "100%",
	overflow: "hidden",
	position: "relative",
	zIndex: 0
};
function gd({ nodes: e, edges: t, defaultNodes: n, defaultEdges: r, className: i, nodeTypes: a, edgeTypes: o, onNodeClick: s, onEdgeClick: c, onInit: l, onMove: u, onMoveStart: d, onMoveEnd: f, onConnect: p, onConnectStart: m, onConnectEnd: h, onClickConnectStart: g, onClickConnectEnd: _, onNodeMouseEnter: v, onNodeMouseMove: b, onNodeMouseLeave: C, onNodeContextMenu: w, onNodeDoubleClick: T, onNodeDragStart: E, onNodeDrag: D, onNodeDragStop: O, onNodesDelete: k, onEdgesDelete: A, onDelete: j, onSelectionChange: M, onSelectionDragStart: ee, onSelectionDrag: N, onSelectionDragStop: te, onSelectionContextMenu: P, onSelectionStart: F, onSelectionEnd: ne, onBeforeDelete: re, connectionMode: I, connectionLineType: L = Da.Bezier, connectionLineStyle: R, connectionLineComponent: z, connectionLineContainerStyle: ie, deleteKeyCode: ae = "Backspace", selectionKeyCode: oe = "Shift", selectionOnDrag: se = !1, selectionMode: ce = Ta.Full, panActivationKeyCode: le = "Space", multiSelectionKeyCode: ue = lo() ? "Meta" : "Control", zoomActivationKeyCode: de = lo() ? "Meta" : "Control", snapToGrid: fe, snapGrid: pe, onlyRenderVisibleElements: me = !1, selectNodesOnDrag: he, nodesDraggable: ge, autoPanOnNodeFocus: _e, nodesConnectable: ve, nodesFocusable: ye, nodeOrigin: be = zc, edgesFocusable: xe, edgesReconnectable: Se, elementsSelectable: Ce = !0, defaultViewport: we = Bc, minZoom: Te = .5, maxZoom: Ee = 2, translateExtent: De = ba, preventScrolling: Oe = !0, nodeExtent: ke, defaultMarkerColor: Ae = "#b1b1b7", zoomOnScroll: je = !0, zoomOnPinch: Me = !0, panOnScroll: Ne = !1, panOnScrollSpeed: Pe = .5, panOnScrollMode: Fe = wa.Free, zoomOnDoubleClick: Ie = !0, panOnDrag: Le = !0, onPaneClick: Re, onPaneMouseEnter: ze, onPaneMouseMove: Be, onPaneMouseLeave: Ve, onPaneScroll: He, onPaneContextMenu: Ue, paneClickDistance: We = 1, nodeClickDistance: Ge = 0, children: Ke, onReconnect: qe, onReconnectStart: Je, onReconnectEnd: Ye, onEdgeContextMenu: Xe, onEdgeDoubleClick: Ze, onEdgeMouseEnter: Qe, onEdgeMouseMove: $e, onEdgeMouseLeave: et, reconnectRadius: tt = 10, onNodesChange: nt, onEdgesChange: rt, noDragClassName: it = "nodrag", noWheelClassName: at = "nowheel", noPanClassName: ot = "nopan", fitView: st, fitViewOptions: ct, connectOnClick: lt, attributionPosition: ut, proOptions: dt, defaultEdgeOptions: ft, elevateNodesOnSelect: pt = !0, elevateEdgesOnSelect: mt = !1, disableKeyboardA11y: ht = !1, autoPanOnConnect: gt, autoPanOnNodeDrag: _t, autoPanOnSelection: vt = !0, autoPanSpeed: yt, connectionRadius: bt, isValidConnection: xt, onError: St, style: Ct, id: wt, nodeDragThreshold: Tt, connectionDragThreshold: Et, viewport: Dt, onViewportChange: Ot, width: kt, height: At, colorMode: jt = "light", debug: Mt, onScroll: Nt, ariaLabelConfig: Pt, zIndexMode: Ft = "basic", ...It }, Lt) {
	let Rt = wt || "1", zt = Kc(jt), Bt = (0, y.useCallback)((e) => {
		e.currentTarget.scrollTo({
			top: 0,
			left: 0,
			behavior: "instant"
		}), Nt?.(e);
	}, [Nt]);
	return (0, x.jsx)("div", {
		"data-testid": "rf__wrapper",
		...It,
		onScroll: Bt,
		style: {
			...Ct,
			...hd
		},
		ref: Lt,
		className: S([
			"react-flow",
			i,
			zt
		]),
		id: wt,
		role: "application",
		children: (0, x.jsxs)(md, {
			nodes: e,
			edges: t,
			width: kt,
			height: At,
			fitView: st,
			fitViewOptions: ct,
			minZoom: Te,
			maxZoom: Ee,
			nodeOrigin: be,
			nodeExtent: ke,
			zIndexMode: Ft,
			children: [
				(0, x.jsx)(Wc, {
					nodes: e,
					edges: t,
					defaultNodes: n,
					defaultEdges: r,
					onConnect: p,
					onConnectStart: m,
					onConnectEnd: h,
					onClickConnectStart: g,
					onClickConnectEnd: _,
					nodesDraggable: ge,
					autoPanOnNodeFocus: _e,
					nodesConnectable: ve,
					nodesFocusable: ye,
					edgesFocusable: xe,
					edgesReconnectable: Se,
					elementsSelectable: Ce,
					elevateNodesOnSelect: pt,
					elevateEdgesOnSelect: mt,
					minZoom: Te,
					maxZoom: Ee,
					nodeExtent: ke,
					onNodesChange: nt,
					onEdgesChange: rt,
					snapToGrid: fe,
					snapGrid: pe,
					connectionMode: I,
					translateExtent: De,
					connectOnClick: lt,
					defaultEdgeOptions: ft,
					fitView: st,
					fitViewOptions: ct,
					onNodesDelete: k,
					onEdgesDelete: A,
					onDelete: j,
					onNodeDragStart: E,
					onNodeDrag: D,
					onNodeDragStop: O,
					onSelectionDrag: N,
					onSelectionDragStart: ee,
					onSelectionDragStop: te,
					onMove: u,
					onMoveStart: d,
					onMoveEnd: f,
					noPanClassName: ot,
					nodeOrigin: be,
					rfId: Rt,
					autoPanOnConnect: gt,
					autoPanOnNodeDrag: _t,
					autoPanSpeed: yt,
					onError: St,
					connectionRadius: bt,
					isValidConnection: xt,
					selectNodesOnDrag: he,
					nodeDragThreshold: Tt,
					connectionDragThreshold: Et,
					onBeforeDelete: re,
					debug: Mt,
					ariaLabelConfig: Pt,
					zIndexMode: Ft
				}),
				(0, x.jsx)(ld, {
					onInit: l,
					onNodeClick: s,
					onEdgeClick: c,
					onNodeMouseEnter: v,
					onNodeMouseMove: b,
					onNodeMouseLeave: C,
					onNodeContextMenu: w,
					onNodeDoubleClick: T,
					nodeTypes: a,
					edgeTypes: o,
					connectionLineType: L,
					connectionLineStyle: R,
					connectionLineComponent: z,
					connectionLineContainerStyle: ie,
					selectionKeyCode: oe,
					selectionOnDrag: se,
					selectionMode: ce,
					deleteKeyCode: ae,
					multiSelectionKeyCode: ue,
					panActivationKeyCode: le,
					zoomActivationKeyCode: de,
					onlyRenderVisibleElements: me,
					defaultViewport: we,
					translateExtent: De,
					minZoom: Te,
					maxZoom: Ee,
					preventScrolling: Oe,
					zoomOnScroll: je,
					zoomOnPinch: Me,
					zoomOnDoubleClick: Ie,
					panOnScroll: Ne,
					panOnScrollSpeed: Pe,
					panOnScrollMode: Fe,
					panOnDrag: Le,
					autoPanOnSelection: vt,
					onPaneClick: Re,
					onPaneMouseEnter: ze,
					onPaneMouseMove: Be,
					onPaneMouseLeave: Ve,
					onPaneScroll: He,
					onPaneContextMenu: Ue,
					paneClickDistance: We,
					nodeClickDistance: Ge,
					onSelectionContextMenu: P,
					onSelectionStart: F,
					onSelectionEnd: ne,
					onReconnect: qe,
					onReconnectStart: Je,
					onReconnectEnd: Ye,
					onEdgeContextMenu: Xe,
					onEdgeDoubleClick: Ze,
					onEdgeMouseEnter: Qe,
					onEdgeMouseMove: $e,
					onEdgeMouseLeave: et,
					reconnectRadius: tt,
					defaultMarkerColor: Ae,
					noDragClassName: it,
					noWheelClassName: at,
					noPanClassName: ot,
					rfId: Rt,
					disableKeyboardA11y: ht,
					nodeExtent: ke,
					viewport: Dt,
					onViewportChange: Ot,
					nodesDraggable: ge
				}),
				(0, x.jsx)(Rc, { onSelectionChange: M }),
				Ke,
				(0, x.jsx)(Mc, {
					proOptions: dt,
					position: ut
				}),
				(0, x.jsx)(kc, {
					rfId: Rt,
					disableKeyboardA11y: ht
				})
			]
		})
	});
}
var _d = ul(gd);
ya.error014();
function vd({ dimensions: e, lineWidth: t, variant: n, className: r }) {
	return (0, x.jsx)("path", {
		strokeWidth: t,
		d: `M${e[0] / 2} 0 V${e[1]} M0 ${e[1] / 2} H${e[0]}`,
		className: S([
			"react-flow__background-pattern",
			n,
			r
		])
	});
}
function yd({ radius: e, className: t }) {
	return (0, x.jsx)("circle", {
		cx: e,
		cy: e,
		r: e,
		className: S([
			"react-flow__background-pattern",
			"dots",
			t
		])
	});
}
var bd;
(function(e) {
	e.Lines = "lines", e.Dots = "dots", e.Cross = "cross";
})(bd ||= {});
var $ = {
	[bd.Dots]: 1,
	[bd.Lines]: 1,
	[bd.Cross]: 6
}, xd = (e) => ({
	transform: e.transform,
	patternId: `pattern-${e.rfId}`
});
function Sd({ id: e, variant: t = bd.Dots, gap: n = 20, size: r, lineWidth: i = 1, offset: a = 0, color: o, bgColor: s, style: c, className: l, patternClassName: u }) {
	let d = (0, y.useRef)(null), { transform: f, patternId: p } = W(xd, _c), m = r || $[t], h = t === bd.Dots, g = t === bd.Cross, _ = Array.isArray(n) ? n : [n, n], v = [_[0] * f[2] || 1, _[1] * f[2] || 1], b = m * f[2], C = Array.isArray(a) ? a : [a, a], w = g ? [b, b] : v, T = [C[0] * f[2] + w[0] / 2, C[1] * f[2] + w[1] / 2], E = `${p}${e || ""}`;
	return (0, x.jsxs)("svg", {
		className: S(["react-flow__background", l]),
		style: {
			...c,
			...Cl,
			"--xy-background-color-props": s,
			"--xy-background-pattern-color-props": o
		},
		ref: d,
		"data-testid": "rf__background",
		children: [(0, x.jsx)("pattern", {
			id: E,
			x: f[0] % v[0],
			y: f[1] % v[1],
			width: v[0],
			height: v[1],
			patternUnits: "userSpaceOnUse",
			patternTransform: `translate(-${T[0]},-${T[1]})`,
			children: h ? (0, x.jsx)(yd, {
				radius: b / 2,
				className: u
			}) : (0, x.jsx)(vd, {
				dimensions: w,
				lineWidth: i,
				variant: t,
				className: u
			})
		}), (0, x.jsx)("rect", {
			x: "0",
			y: "0",
			width: "100%",
			height: "100%",
			fill: `url(#${E})`
		})]
	});
}
Sd.displayName = "Background", (0, y.memo)(Sd);
function Cd() {
	return (0, x.jsx)("svg", {
		xmlns: "http://www.w3.org/2000/svg",
		viewBox: "0 0 32 32",
		children: (0, x.jsx)("path", { d: "M32 18.133H18.133V32h-4.266V18.133H0v-4.266h13.867V0h4.266v13.867H32z" })
	});
}
function wd() {
	return (0, x.jsx)("svg", {
		xmlns: "http://www.w3.org/2000/svg",
		viewBox: "0 0 32 5",
		children: (0, x.jsx)("path", { d: "M0 0h32v4.2H0z" })
	});
}
function Td() {
	return (0, x.jsx)("svg", {
		xmlns: "http://www.w3.org/2000/svg",
		viewBox: "0 0 32 30",
		children: (0, x.jsx)("path", { d: "M3.692 4.63c0-.53.4-.938.939-.938h5.215V0H4.708C2.13 0 0 2.054 0 4.63v5.216h3.692V4.631zM27.354 0h-5.2v3.692h5.17c.53 0 .984.4.984.939v5.215H32V4.631A4.624 4.624 0 0027.354 0zm.954 24.83c0 .532-.4.94-.939.94h-5.215v3.768h5.215c2.577 0 4.631-2.13 4.631-4.707v-5.139h-3.692v5.139zm-23.677.94c-.531 0-.939-.4-.939-.94v-5.138H0v5.139c0 2.577 2.13 4.707 4.708 4.707h5.138V25.77H4.631z" })
	});
}
function Ed() {
	return (0, x.jsx)("svg", {
		xmlns: "http://www.w3.org/2000/svg",
		viewBox: "0 0 25 32",
		children: (0, x.jsx)("path", { d: "M21.333 10.667H19.81V7.619C19.81 3.429 16.38 0 12.19 0 8 0 4.571 3.429 4.571 7.619v3.048H3.048A3.056 3.056 0 000 13.714v15.238A3.056 3.056 0 003.048 32h18.285a3.056 3.056 0 003.048-3.048V13.714a3.056 3.056 0 00-3.048-3.047zM12.19 24.533a3.056 3.056 0 01-3.047-3.047 3.056 3.056 0 013.047-3.048 3.056 3.056 0 013.048 3.048 3.056 3.056 0 01-3.048 3.047zm4.724-13.866H7.467V7.619c0-2.59 2.133-4.724 4.723-4.724 2.591 0 4.724 2.133 4.724 4.724v3.048z" })
	});
}
function Dd() {
	return (0, x.jsx)("svg", {
		xmlns: "http://www.w3.org/2000/svg",
		viewBox: "0 0 25 32",
		children: (0, x.jsx)("path", { d: "M21.333 10.667H19.81V7.619C19.81 3.429 16.38 0 12.19 0c-4.114 1.828-1.37 2.133.305 2.438 1.676.305 4.42 2.59 4.42 5.181v3.048H3.047A3.056 3.056 0 000 13.714v15.238A3.056 3.056 0 003.048 32h18.285a3.056 3.056 0 003.048-3.048V13.714a3.056 3.056 0 00-3.048-3.047zM12.19 24.533a3.056 3.056 0 01-3.047-3.047 3.056 3.056 0 013.047-3.048 3.056 3.056 0 013.048 3.048 3.056 3.056 0 01-3.048 3.047z" })
	});
}
function Od({ children: e, className: t, ...n }) {
	return (0, x.jsx)("button", {
		type: "button",
		className: S(["react-flow__controls-button", t]),
		...n,
		children: e
	});
}
var kd = (e) => ({
	isInteractive: e.nodesDraggable || e.nodesConnectable || e.elementsSelectable,
	minZoomReached: e.transform[2] <= e.minZoom,
	maxZoomReached: e.transform[2] >= e.maxZoom,
	ariaLabelConfig: e.ariaLabelConfig
});
function Ad({ style: e, showZoom: t = !0, showFitView: n = !0, showInteractive: r = !0, fitViewOptions: i, onZoomIn: a, onZoomOut: o, onFitView: s, onInteractiveChange: c, className: l, children: u, position: d = "bottom-left", orientation: f = "vertical", "aria-label": p }) {
	let m = G(), { isInteractive: h, minZoomReached: g, maxZoomReached: _, ariaLabelConfig: v } = W(kd, _c), { zoomIn: y, zoomOut: b, fitView: C } = vl();
	return (0, x.jsxs)(Ac, {
		className: S([
			"react-flow__controls",
			f === "horizontal" ? "horizontal" : "vertical",
			l
		]),
		position: d,
		style: e,
		"data-testid": "rf__controls",
		"aria-label": p ?? v["controls.ariaLabel"],
		children: [
			t && (0, x.jsxs)(x.Fragment, { children: [(0, x.jsx)(Od, {
				onClick: () => {
					y(), a?.();
				},
				className: "react-flow__controls-zoomin",
				title: v["controls.zoomIn.ariaLabel"],
				"aria-label": v["controls.zoomIn.ariaLabel"],
				disabled: _,
				children: (0, x.jsx)(Cd, {})
			}), (0, x.jsx)(Od, {
				onClick: () => {
					b(), o?.();
				},
				className: "react-flow__controls-zoomout",
				title: v["controls.zoomOut.ariaLabel"],
				"aria-label": v["controls.zoomOut.ariaLabel"],
				disabled: g,
				children: (0, x.jsx)(wd, {})
			})] }),
			n && (0, x.jsx)(Od, {
				className: "react-flow__controls-fitview",
				onClick: () => {
					C(i), s?.();
				},
				title: v["controls.fitView.ariaLabel"],
				"aria-label": v["controls.fitView.ariaLabel"],
				children: (0, x.jsx)(Td, {})
			}),
			r && (0, x.jsx)(Od, {
				className: "react-flow__controls-interactive",
				onClick: () => {
					m.setState({
						nodesDraggable: !h,
						nodesConnectable: !h,
						elementsSelectable: !h
					}), c?.(!h);
				},
				title: v["controls.interactive.ariaLabel"],
				"aria-label": v["controls.interactive.ariaLabel"],
				children: h ? (0, x.jsx)(Dd, {}) : (0, x.jsx)(Ed, {})
			}),
			u
		]
	});
}
Ad.displayName = "Controls", (0, y.memo)(Ad);
function jd({ id: e, x: t, y: n, width: r, height: i, style: a, color: o, strokeColor: s, strokeWidth: c, className: l, borderRadius: u, shapeRendering: d, selected: f, onClick: p }) {
	let { background: m, backgroundColor: h } = a || {}, g = o || m || h;
	return (0, x.jsx)("rect", {
		className: S([
			"react-flow__minimap-node",
			{ selected: f },
			l
		]),
		x: t,
		y: n,
		rx: u,
		ry: u,
		width: r,
		height: i,
		style: {
			fill: g,
			stroke: s,
			strokeWidth: c
		},
		shapeRendering: d,
		onClick: p ? (t) => p(t, e) : void 0
	});
}
var Md = (0, y.memo)(jd), Nd = (e) => e.nodes.map((e) => e.id), Pd = (e) => e instanceof Function ? e : () => e;
function Fd({ nodeStrokeColor: e, nodeColor: t, nodeClassName: n = "", nodeBorderRadius: r = 5, nodeStrokeWidth: i, nodeComponent: a = Md, onClick: o }) {
	let s = W(Nd, _c), c = Pd(t), l = Pd(e), u = Pd(n), d = typeof window > "u" || window.chrome ? "crispEdges" : "geometricPrecision";
	return (0, x.jsx)(x.Fragment, { children: s.map((e) => (0, x.jsx)(Ld, {
		id: e,
		nodeColorFunc: c,
		nodeStrokeColorFunc: l,
		nodeClassNameFunc: u,
		nodeBorderRadius: r,
		nodeStrokeWidth: i,
		NodeComponent: a,
		onClick: o,
		shapeRendering: d
	}, e)) });
}
function Id({ id: e, nodeColorFunc: t, nodeStrokeColorFunc: n, nodeClassNameFunc: r, nodeBorderRadius: i, nodeStrokeWidth: a, shapeRendering: o, NodeComponent: s, onClick: c }) {
	let { node: l, x: u, y: d, width: f, height: p } = W((t) => {
		let n = t.nodeLookup.get(e);
		if (!n) return {
			node: void 0,
			x: 0,
			y: 0,
			width: 0,
			height: 0
		};
		let r = n.internals.userNode, { x: i, y: a } = n.internals.positionAbsolute, { width: o, height: s } = fo(r);
		return {
			node: r,
			x: i,
			y: a,
			width: o,
			height: s
		};
	}, _c);
	return !l || l.hidden || !po(l) ? null : (0, x.jsx)(s, {
		x: u,
		y: d,
		width: f,
		height: p,
		style: l.style,
		selected: !!l.selected,
		className: r(l),
		color: t(l),
		borderRadius: i,
		strokeColor: n(l),
		strokeWidth: a,
		shapeRendering: o,
		onClick: c,
		id: l.id
	});
}
var Ld = (0, y.memo)(Id), Rd = (0, y.memo)(Fd), zd = 200, Bd = 150, Vd = (e) => !e.hidden, Hd = (e) => {
	let t = {
		x: -e.transform[0] / e.transform[2],
		y: -e.transform[1] / e.transform[2],
		width: e.width / e.transform[2],
		height: e.height / e.transform[2]
	}, n = !1;
	for (let t of e.nodeLookup.values()) if (!t.hidden) {
		n = !0;
		break;
	}
	return {
		viewBB: t,
		boundingRect: n ? Xa(Fa(e.nodeLookup, { filter: Vd }), t) : t,
		rfId: e.rfId,
		panZoom: e.panZoom,
		translateExtent: e.translateExtent,
		flowWidth: e.width,
		flowHeight: e.height,
		ariaLabelConfig: e.ariaLabelConfig
	};
}, Ud = (e, t) => e.x === t.x && e.y === t.y && e.width === t.width && e.height === t.height, Wd = (e, t) => Ud(e.viewBB, t.viewBB) && Ud(e.boundingRect, t.boundingRect) && e.rfId === t.rfId && e.panZoom === t.panZoom && e.translateExtent === t.translateExtent && e.flowWidth === t.flowWidth && e.flowHeight === t.flowHeight && e.ariaLabelConfig === t.ariaLabelConfig, Gd = "react-flow__minimap-desc";
function Kd({ style: e, className: t, nodeStrokeColor: n, nodeColor: r, nodeClassName: i = "", nodeBorderRadius: a = 5, nodeStrokeWidth: o, nodeComponent: s, bgColor: c, maskColor: l, maskStrokeColor: u, maskStrokeWidth: d, position: f = "bottom-right", onClick: p, onNodeClick: m, pannable: h = !1, zoomable: g = !1, ariaLabel: _, inversePan: v, zoomStep: b = 1, offsetScale: C = 5 }) {
	let w = G(), T = (0, y.useRef)(null), { boundingRect: E, panZoom: D, viewBB: O, rfId: k, translateExtent: A, flowWidth: j, flowHeight: M, ariaLabelConfig: ee } = W(Hd, Wd), N = e?.width ?? zd, te = e?.height ?? Bd, P = E.width / N, F = E.height / te, ne = Math.max(P, F), re = ne * N, I = ne * te, L = C * ne, R = E.x - (re - E.width) / 2 - L, z = E.y - (I - E.height) / 2 - L, ie = re + L * 2, ae = I + L * 2, oe = `${Gd}-${k}`, se = (0, y.useRef)(0), ce = (0, y.useRef)();
	se.current = ne, (0, y.useEffect)(() => {
		let e = w.getState().panZoom;
		if (T.current && e) return ce.current = Ms({
			domNode: T.current,
			panZoom: e,
			getTransform: () => w.getState().transform,
			getViewScale: () => se.current
		}), () => {
			ce.current?.destroy();
		};
	}, [D]), (0, y.useEffect)(() => {
		ce.current?.update({
			translateExtent: A,
			width: j,
			height: M,
			inversePan: v,
			pannable: h,
			zoomStep: b,
			zoomable: g
		});
	}, [
		h,
		g,
		v,
		b,
		A,
		j,
		M
	]);
	let le = p ? (e) => {
		let [t, n] = ce.current?.pointer(e) || [0, 0];
		p(e, {
			x: t,
			y: n
		});
	} : void 0, ue = (0, y.useCallback)((e, t) => {
		let n = w.getState().nodeLookup.get(t).internals.userNode;
		m?.(e, n);
	}, [m]), de = m ? ue : void 0, fe = _ ?? ee["minimap.ariaLabel"];
	return (0, x.jsx)(Ac, {
		position: f,
		style: {
			...e,
			"--xy-minimap-background-color-props": typeof c == "string" ? c : void 0,
			"--xy-minimap-mask-background-color-props": typeof l == "string" ? l : void 0,
			"--xy-minimap-mask-stroke-color-props": typeof u == "string" ? u : void 0,
			"--xy-minimap-mask-stroke-width-props": typeof d == "number" ? d * ne : void 0,
			"--xy-minimap-node-background-color-props": typeof r == "string" ? r : void 0,
			"--xy-minimap-node-stroke-color-props": typeof n == "string" ? n : void 0,
			"--xy-minimap-node-stroke-width-props": typeof o == "number" ? o : void 0
		},
		className: S(["react-flow__minimap", t]),
		"data-testid": "rf__minimap",
		children: (0, x.jsxs)("svg", {
			width: N,
			height: te,
			viewBox: `${R} ${z} ${ie} ${ae}`,
			className: "react-flow__minimap-svg",
			role: "img",
			"aria-labelledby": oe,
			ref: T,
			onClick: le,
			children: [
				fe && (0, x.jsx)("title", {
					id: oe,
					children: fe
				}),
				(0, x.jsx)(Rd, {
					onClick: de,
					nodeColor: r,
					nodeStrokeColor: n,
					nodeBorderRadius: a,
					nodeClassName: i,
					nodeStrokeWidth: o,
					nodeComponent: s
				}),
				(0, x.jsx)("path", {
					className: "react-flow__minimap-mask",
					d: `M${R - L},${z - L}h${ie + L * 2}v${ae + L * 2}h${-ie - L * 2}z
        M${O.x},${O.y}h${O.width}v${O.height}h${-O.width}z`,
					fillRule: "evenodd",
					pointerEvents: "none"
				})
			]
		})
	});
}
Kd.displayName = "MiniMap", (0, y.memo)(Kd);
var qd = (e) => (t) => e ? `${Math.max(1 / t.transform[2], 1)}` : void 0, Jd = {
	[qs.Line]: "right",
	[qs.Handle]: "bottom-right"
};
function Yd({ nodeId: e, position: t, variant: n = qs.Handle, className: r, style: i = void 0, children: a, color: o, minWidth: s = 10, minHeight: c = 10, maxWidth: l = Number.MAX_VALUE, maxHeight: u = Number.MAX_VALUE, keepAspectRatio: d = !1, resizeDirection: f, autoScale: p = !0, shouldResize: m, onResizeStart: h, onResize: g, onResizeEnd: _ }) {
	let v = Ml(), b = typeof e == "string" ? e : v, C = G(), w = (0, y.useRef)(null), T = n === qs.Handle, E = W((0, y.useCallback)(qd(T && p), [T, p]), _c), D = (0, y.useRef)(null), O = t ?? Jd[n];
	(0, y.useEffect)(() => {
		if (w.current && b) return D.current ||= ic({
			domNode: w.current,
			nodeId: b,
			getStoreItems: () => {
				let { nodeLookup: e, transform: t, snapGrid: n, snapToGrid: r, nodeOrigin: i, domNode: a } = C.getState();
				return {
					nodeLookup: e,
					transform: t,
					snapGrid: n,
					snapToGrid: r,
					nodeOrigin: i,
					paneDomNode: a
				};
			},
			onChange: (e, t) => {
				let { triggerNodeChanges: n, nodeLookup: r, parentLookup: i, nodeOrigin: a } = C.getState(), o = [], s = {
					x: e.x,
					y: e.y
				}, c = r.get(b);
				if (c && c.expandParent && c.parentId) {
					let t = c.origin ?? a, n = e.width ?? c.measured.width ?? 0, l = e.height ?? c.measured.height ?? 0, u = ds([{
						id: c.id,
						parentId: c.parentId,
						rect: {
							width: n,
							height: l,
							...mo({
								x: e.x ?? c.position.x,
								y: e.y ?? c.position.y
							}, {
								width: n,
								height: l
							}, c.parentId, r, t)
						}
					}], r, i, a);
					o.push(...u), s.x = e.x ? Math.max(t[0] * n, e.x) : void 0, s.y = e.y ? Math.max(t[1] * l, e.y) : void 0;
				}
				if (s.x !== void 0 && s.y !== void 0) {
					let e = {
						id: b,
						type: "position",
						position: { ...s }
					};
					o.push(e);
				}
				if (e.width !== void 0 && e.height !== void 0) {
					let t = {
						id: b,
						type: "dimensions",
						resizing: !0,
						setAttributes: f ? f === "horizontal" ? "width" : "height" : !0,
						dimensions: {
							width: e.width,
							height: e.height
						}
					};
					o.push(t);
				}
				for (let e of t) {
					let t = {
						...e,
						type: "position"
					};
					o.push(t);
				}
				n(o);
			},
			onEnd: ({ width: e, height: t }) => {
				let n = {
					id: b,
					type: "dimensions",
					resizing: !1,
					dimensions: {
						width: e,
						height: t
					}
				};
				C.getState().triggerNodeChanges([n]);
			}
		}), D.current.update({
			controlPosition: O,
			boundaries: {
				minWidth: s,
				minHeight: c,
				maxWidth: l,
				maxHeight: u
			},
			keepAspectRatio: d,
			resizeDirection: f,
			onResizeStart: h,
			onResize: g,
			onResizeEnd: _,
			shouldResize: m
		}), () => {
			D.current?.destroy();
		};
	}, [
		O,
		s,
		c,
		l,
		u,
		d,
		h,
		g,
		_,
		m
	]);
	let k = O.split("-");
	return (0, x.jsx)("div", {
		className: S([
			"react-flow__resize-control",
			"nodrag",
			...k,
			n,
			r
		]),
		ref: w,
		style: {
			...i,
			scale: E,
			...o && { [T ? "backgroundColor" : "borderColor"]: o }
		},
		children: a
	});
}
(0, y.memo)(Yd);
// src/topology.jsx
var Xd = {
	frontend: "Front end",
	project: "Project",
	"database-server": "Database server",
	database: "Database",
	gateway: "Entry",
	namespace: "Namespace",
	service: "Service",
	deployment: "Deployment",
	statefulset: "StatefulSet",
	volume: "Volume",
	config: "ConfigMap",
	secret: "Secret"
}, Zd = {
	ready: "Ready",
	starting: "Starting",
	failing: "Failing",
	stopped: "Stopped"
};
function Qd(e) {
	let t = e.detail.split(" · "), n = e.state === "failing" ? t.find((e) => /^[A-Z][A-Za-z]+$/.test(e)) : null;
	return `${Zd[e.state] ?? e.state} · ${n ?? t[0].replace(" ready", "")}`;
}
var $d = /* @__PURE__ */ new Set([
	"frontend",
	"database-server",
	"gateway",
	"statefulset"
]), ef = /* @__PURE__ */ new Set(["config", "secret"]), tf = {
	full: {
		w: 168,
		h: 64,
		gapX: 112,
		gapY: 24,
		pad: 28
	},
	compact: {
		w: 96,
		h: 30,
		gapX: 22,
		gapY: 8,
		pad: 12
	}
};
function nf({ data: e }) {
	let { node: t, compact: n, vertical: r, status: i } = e;
	return /* @__PURE__ */ (0, x.jsxs)("div", {
		className: `tn tn-${t.kind}${$d.has(t.kind) ? " tn-accent" : ""}${ef.has(t.kind) ? " tn-attachment" : ""}${i ? ` tn-live tn-live-${i.state}` : ""}`,
		children: [
			/* @__PURE__ */ (0, x.jsx)(Bl, {
				type: "target",
				position: r ? V.Top : V.Left,
				isConnectable: !1
			}),
			!n && /* @__PURE__ */ (0, x.jsx)("span", {
				className: "tn-kind",
				children: Xd[t.kind] ?? t.kind
			}),
			/* @__PURE__ */ (0, x.jsxs)("span", {
				className: "tn-label",
				title: t.title,
				children: [i && /* @__PURE__ */ (0, x.jsx)("span", {
					className: "tn-dot",
					"aria-hidden": "true"
				}), t.label]
			}),
			!n && t.detail && /* @__PURE__ */ (0, x.jsx)("span", {
				className: "tn-detail",
				children: t.detail
			}),
			!n && i && /* @__PURE__ */ (0, x.jsx)("span", {
				className: "tn-status",
				title: i.detail,
				children: Qd(i)
			}),
			/* @__PURE__ */ (0, x.jsx)(Bl, {
				type: "source",
				position: r ? V.Bottom : V.Right,
				isConnectable: !1
			})
		]
	});
}
function rf({ data: e }) {
	return /* @__PURE__ */ (0, x.jsxs)("div", {
		className: "tn-namespace",
		children: [!e.compact && /* @__PURE__ */ (0, x.jsx)("span", {
			className: "tn-kind",
			children: "Namespace"
		}), /* @__PURE__ */ (0, x.jsx)("span", {
			className: "tn-namespace-label",
			children: e.node.label
		})]
	});
}
var af = {
	topology: nf,
	namespace: rf
};
function of(e, t) {
	let n = new Map(e.map((e) => [e.id, 0]));
	for (let r = 0; r < e.length; r++) for (let e of t) n.has(e.from) && n.has(e.to) && n.set(e.to, Math.max(n.get(e.to), n.get(e.from) + 1));
	let r = /* @__PURE__ */ new Map();
	return new Map(e.map((e) => {
		let t = n.get(e.id), i = r.get(t) ?? 0;
		return r.set(t, i + 1), [e.id, {
			col: t,
			row: i
		}];
	}));
}
function sf(e, t) {
	let n = {
		gateway: 0,
		service: 1,
		deployment: 2,
		statefulset: 2,
		volume: 3
	}, r = /* @__PURE__ */ new Map(), i = (e, n) => t.find((t) => t.from === e && t.label === n)?.to, a = 0;
	for (let t of e.filter((e) => e.kind === "service")) {
		r.set(t.id, {
			col: 1,
			row: a
		});
		let e = i(t.id, "selects");
		if (e && !r.has(e)) {
			r.set(e, {
				col: 2,
				row: a
			});
			let t = i(e, "mounts");
			t && r.set(t, {
				col: 3,
				row: a
			});
		}
		a++;
	}
	for (let t of e.filter((e) => !r.has(e.id))) r.set(t.id, {
		col: n[t.kind] ?? 2,
		row: t.kind === "gateway" ? 0 : a++
	});
	return r;
}
function cf(e, t, n = !1, r = null, i = 0) {
	let a = t ? tf.compact : tf.full, o = r && !t ? {
		...a,
		w: a.w + 24,
		h: a.h + 16
	} : a, s = e.nodes.filter((e) => !(t && ef.has(e.kind))), c = new Set(s.map((e) => e.id)), l = e.edges.filter((e) => c.has(e.from) && c.has(e.to)), u = s.find((e) => e.kind === "namespace"), d = s.filter((e) => e !== u), f = /* @__PURE__ */ new Map(), p = [];
	for (let e of d) {
		let t = ef.has(e.kind) ? l.find((t) => t.to === e.id)?.from : null;
		t ? f.set(t, [...f.get(t) ?? [], e]) : p.push(e);
	}
	let m = u ? sf(p, l) : of(p, l), h = u && !t ? o.gapX * .6 : o.gapX, g = o.h * .62, _ = (e) => o.h + (f.get(e)?.length ? g + 6 : 0), v = (e) => (o.w - 12 - 6 * (e - 1)) / Math.max(e, 1), y = /* @__PURE__ */ new Map();
	for (let e of p) {
		let { row: t } = m.get(e.id);
		y.set(t, Math.max(y.get(t) ?? 0, _(e.id)));
	}
	let b = /* @__PURE__ */ new Map(), x = 0;
	for (let e of [...y.keys()].sort((e, t) => e - t)) b.set(e, x), x += y.get(e) + o.gapY;
	let S = /* @__PURE__ */ new Map();
	if (n) {
		let e = o.w - 24, t = f.size > 0, n = o.w + (t ? e + 8 : 0) + 32, r = /* @__PURE__ */ new Map();
		for (let e of p) {
			let { col: t } = m.get(e.id), n = Math.max(o.h, (f.get(e.id)?.length ?? 0) * (g + 6));
			r.set(t, Math.max(r.get(t) ?? 0, n));
		}
		let a = /* @__PURE__ */ new Map(), s = 0;
		for (let e of [...r.keys()].sort((e, t) => e - t)) a.set(e, s), s += r.get(e) + o.gapY * 2.5 + (u && e === 0 ? o.pad + 44 : 0);
		let c = Math.max(1, Math.floor((i || Infinity) / n)), l = Math.min(...[...a.keys()].filter((e) => e > 0), Infinity), d = s - (Number.isFinite(l) ? a.get(l) : 0) + o.gapY;
		for (let e of p) {
			let { col: t, row: r } = m.get(e.id), i = t === 0 && u ? 0 : Math.floor(r / c), s = (t === 0 && u ? r : r % c) * n;
			S.set(e.id, {
				x: s,
				y: a.get(t) + i * d
			});
			for (let [n, r] of (f.get(e.id) ?? []).entries()) S.set(r.id, {
				x: s + o.w + 8,
				y: a.get(t) + i * d + n * (g + 6),
				small: !0
			});
		}
	} else for (let e of p) {
		let { col: t, row: n } = m.get(e.id), r = t * (o.w + h), i = b.get(n);
		S.set(e.id, {
			x: r,
			y: i
		});
		let a = f.get(e.id) ?? [];
		for (let [e, t] of a.entries()) S.set(t.id, {
			x: r + 12 + e * (v(a.length) + 6),
			y: i + o.h + 6,
			small: !0,
			width: v(a.length)
		});
	}
	let C = [];
	if (u) {
		let e = d.filter((e) => e.parent === u.id && S.has(e.id)), r = Math.min(...e.map((e) => S.get(e.id).x)), i = Math.min(...e.map((e) => S.get(e.id).y)), a = Math.max(...e.map((e) => S.get(e.id).x + (S.get(e.id).width ?? (S.get(e.id).small ? o.w - 24 : o.w)))), s = Math.max(...e.map((e) => S.get(e.id).y + (S.get(e.id).small ? g : o.h))), c = t ? o.pad : o.pad + (n ? 44 : 18);
		C.push({
			id: u.id,
			type: "namespace",
			data: {
				node: u,
				compact: t
			},
			position: {
				x: r - o.pad,
				y: i - c
			},
			style: {
				width: a - r + o.pad * 2,
				height: s - i + c + o.pad
			},
			draggable: !1,
			selectable: !1
		});
		for (let t of e) {
			let e = S.get(t.id);
			e.x -= r - o.pad, e.y += c - i;
		}
	}
	for (let e of d) {
		let i = S.get(e.id);
		if (!i) continue;
		let a = u && e.parent === u.id, s = i.small ? [...f.entries()].find(([, t]) => t.includes(e))?.[0] : null, c = s ? (d.find((e) => e.id === s)?.label ?? "").replace(/-(deployment|statefulset)$/, "") : "", l = e.kind === "volume" ? /^apphost-[0-9a-f]{6,}-(.+)$/.exec(e.label)?.[1] : null, p = c && e.label.startsWith(c + "-") ? {
			...e,
			label: e.label.slice(c.length + 1),
			title: e.label
		} : l ? {
			...e,
			label: l,
			title: e.label
		} : e;
		C.push({
			id: e.id,
			type: "topology",
			data: {
				node: p,
				compact: t,
				vertical: n,
				status: r?.[e.id] ?? null
			},
			position: {
				x: i.x,
				y: i.y
			},
			style: {
				width: i.width ?? (i.small ? o.w - 24 : o.w),
				height: i.small ? g : o.h
			},
			...a ? {
				parentId: u.id,
				extent: "parent"
			} : {},
			draggable: !1,
			selectable: !1
		});
	}
	let w = new Set([...f.values()].flat().map((e) => e.id));
	return {
		flowNodes: C,
		flowEdges: l.filter((e) => e.from !== u?.id && e.to !== u?.id && !w.has(e.to)).map((e, n) => ({
			id: `e${n}`,
			source: e.from,
			target: e.to,
			type: "smoothstep",
			label: t ? void 0 : e.label,
			className: e.style === "dashed" ? "te-dashed" : "te-solid",
			markerEnd: {
				type: Oa.ArrowClosed,
				width: 12,
				height: 12
			},
			focusable: !1,
			selectable: !1
		})),
		width: Math.max(0, ...C.filter((e) => !e.parentId).map((e) => e.position.x + e.style.width)) - Math.min(0, ...C.filter((e) => !e.parentId).map((e) => e.position.x)),
		height: Math.max(0, ...C.filter((e) => !e.parentId).map((e) => e.position.y + e.style.height)) - Math.min(0, ...C.filter((e) => !e.parentId).map((e) => e.position.y))
	};
}
function lf({ data: e, view: t, compact: n, vertical: r, onSize: i, fit: a = 0, available: o = 0 }) {
	let s = e?.[t] ?? {
		nodes: [],
		edges: []
	}, c = e?.live ?? null, { flowNodes: l, flowEdges: u, width: d, height: f } = (0, y.useMemo)(() => cf(s, n, r, c, o), [
		s,
		n,
		r,
		c,
		o
	]);
	return (0, y.useEffect)(() => {
		i?.(d, f);
	}, [
		i,
		d,
		f
	]), /* @__PURE__ */ (0, x.jsx)(_d, {
		nodes: l,
		edges: u,
		nodeTypes: af,
		fitView: !0,
		fitViewOptions: { padding: n ? .08 : .12 },
		minZoom: .2,
		maxZoom: 1.5,
		nodesDraggable: !1,
		nodesConnectable: !1,
		nodesFocusable: !1,
		edgesFocusable: !1,
		elementsSelectable: !1,
		zoomOnScroll: !1,
		zoomOnDoubleClick: !1,
		zoomOnPinch: !n,
		panOnDrag: !n,
		preventScrolling: !1,
		proOptions: { hideAttribution: !0 }
	}, `${t}-${r ? "vertical" : "horizontal"}-${a}`);
}
function uf(e, t) {
	let n = e?.[t] ?? {
		nodes: [],
		edges: []
	}, r = new Map(n.nodes.map((e) => [e.id, e.label])), i = e?.live ?? {};
	return {
		items: n.nodes.map((e) => `${e.label}: ${Xd[e.kind] ?? e.kind}${e.detail ? `, ${e.detail}` : ""}` + (i[e.id] ? `. ${Zd[i[e.id].state] ?? i[e.id].state}, ${i[e.id].detail}` : "")),
		links: n.edges.map((e) => `${r.get(e.from) ?? e.from} to ${r.get(e.to) ?? e.to}${e.label ? ` (${e.label})` : ""}`)
	};
}
function df({ data: e, view: t, compact: n, vertical: r, onSize: i, fit: a, available: o }) {
	if (n) return /* @__PURE__ */ (0, x.jsx)("div", {
		className: "topology-canvas",
		"aria-hidden": "true",
		children: /* @__PURE__ */ (0, x.jsx)(lf, {
			data: e,
			view: t,
			compact: !0
		})
	});
	let { items: s, links: c } = uf(e, t);
	return /* @__PURE__ */ (0, x.jsxs)(x.Fragment, { children: [/* @__PURE__ */ (0, x.jsx)("div", {
		className: "topology-canvas",
		"aria-hidden": "true",
		children: /* @__PURE__ */ (0, x.jsx)(lf, {
			data: e,
			view: t,
			compact: n,
			vertical: r,
			onSize: i,
			fit: a,
			available: o
		})
	}), /* @__PURE__ */ (0, x.jsxs)("div", {
		className: "topology-description",
		children: [
			/* @__PURE__ */ (0, x.jsx)("p", { children: t === "aspire" ? "Aspire resources" : "Cluster objects" }),
			/* @__PURE__ */ (0, x.jsx)("ul", { children: s.map((e) => /* @__PURE__ */ (0, x.jsx)("li", { children: e }, e)) }),
			c.length > 0 && /* @__PURE__ */ (0, x.jsxs)(x.Fragment, { children: [/* @__PURE__ */ (0, x.jsx)("p", { children: "Connections" }), /* @__PURE__ */ (0, x.jsx)("ul", { children: c.map((e) => /* @__PURE__ */ (0, x.jsx)("li", { children: e }, e)) })] })
		]
	})] });
}
function ff(e, t, n = {}) {
	let r = (0, b.createRoot)(e);
	e.classList.add("topology-island");
	let i = {
		data: t,
		view: n.view ?? "aspire",
		compact: !!n.compact
	}, a = () => !i.compact && e.clientWidth > 0 && e.clientWidth < 560, o = a(), s = parseFloat(getComputedStyle(e).height) || 340, c = 0, l = (t, n) => {
		if (i.compact || !t || !n) return;
		let r = Math.min(1, e.clientWidth * .88 / t), a = `${Math.round(Math.min(Math.max(s, n * r / .88), 1100))}px`;
		e.style.height !== a && (e.style.height = a, c++, u());
	}, u = () => {
		e.classList.toggle("topology-compact", i.compact), r.render(/* @__PURE__ */ (0, x.jsx)(y.StrictMode, { children: /* @__PURE__ */ (0, x.jsx)(df, {
			data: i.data,
			view: i.view,
			compact: i.compact,
			vertical: o,
			onSize: l,
			fit: c,
			available: o ? Math.max(0, e.clientWidth * .88 - 32) : 0
		}) }));
	}, d = typeof ResizeObserver == "function" ? new ResizeObserver(() => {
		a() !== o && (o = a(), u());
	}) : null;
	return d?.observe(e), u(), {
		update(e, t = {}) {
			i = {
				data: e ?? i.data,
				view: t.view ?? i.view,
				compact: t.compact ?? i.compact
			}, o = a(), u();
		},
		unmount() {
			d?.disconnect(), r.unmount();
		}
	};
}
export { ff as mountTopology };
