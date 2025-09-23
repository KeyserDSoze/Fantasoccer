var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __export = (target, all) => {
    for (var name in all) __defProp(target, name, {
      get: all[name],
      enumerable: !0
    });
  },
  __copyProps = (to, from, except, desc) => {
    if (from && typeof from == "object" || typeof from == "function") for (let key of __getOwnPropNames(from)) !__hasOwnProp.call(to, key) && key !== except && __defProp(to, key, {
      get: () => from[key],
      enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable
    });
    return to;
  };
var __toCommonJS = mod => __copyProps(__defProp({}, "__esModule", {
  value: !0
}), mod);
var tamagui_config_exports = {};
__export(tamagui_config_exports, {
  config: () => config
});
module.exports = __toCommonJS(tamagui_config_exports);
var import_v4 = require("@tamagui/config/v4"),
  import_tamagui = require("tamagui"),
  import_fonts = require("./fonts.cjs"),
  import_animations = require("./animations.cjs");
const config = (0, import_tamagui.createTamagui)({
  ...import_v4.defaultConfig,
  animations: import_animations.animations,
  fonts: {
    body: import_fonts.bodyFont,
    heading: import_fonts.headingFont
  },
  settings: {
    ...import_v4.defaultConfig.settings,
    onlyAllowShorthands: !1
  }
});