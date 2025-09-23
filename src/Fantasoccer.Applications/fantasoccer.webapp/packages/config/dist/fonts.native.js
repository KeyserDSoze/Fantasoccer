var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: !0 });
}, __copyProps = (to, from, except, desc) => {
  if (from && typeof from == "object" || typeof from == "function")
    for (let key of __getOwnPropNames(from))
      !__hasOwnProp.call(to, key) && key !== except && __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  return to;
};
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: !0 }), mod);
var fonts_exports = {};
__export(fonts_exports, {
  bodyFont: () => bodyFont,
  headingFont: () => headingFont
});
module.exports = __toCommonJS(fonts_exports);
var import_font_inter = require("@tamagui/font-inter"), headingFont = (0, import_font_inter.createInterFont)({
  size: {
    6: 15
  },
  transform: {
    6: "uppercase",
    7: "none"
  },
  weight: {
    6: "400",
    7: "700"
  },
  color: {
    6: "$colorFocus",
    7: "$color"
  },
  letterSpacing: {
    5: 2,
    6: 1,
    7: 0,
    8: -1,
    9: -2,
    10: -3,
    12: -4,
    14: -5,
    15: -6
  },
  face: {
    700: {
      normal: "InterBold"
    }
  }
}), bodyFont = (0, import_font_inter.createInterFont)({
  face: {
    700: {
      normal: "InterBold"
    }
  }
}, {
  sizeSize: function(size) {
    return Math.round(size * 1.1);
  },
  sizeLineHeight: function(size) {
    return Math.round(size * 1.1 + (size > 20, 10));
  }
});
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  bodyFont,
  headingFont
});
//# sourceMappingURL=fonts.js.map
