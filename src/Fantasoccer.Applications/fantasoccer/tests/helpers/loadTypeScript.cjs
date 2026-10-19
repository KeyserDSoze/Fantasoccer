const fs = require('node:fs')
const path = require('node:path')
const vm = require('node:vm')
const ts = require('typescript')

// Execute the real module with explicit external boundaries (network/browser/
// native storage). Each test gets an independent module cache and browser state.
exports.loadTypeScript = (entry, mocks = {}, globals = {}) => {
  const root = path.resolve(__dirname, '../..')
  const cache = new Map()
  function load(filename) {
    if (cache.has(filename)) return cache.get(filename).exports
    const module = { exports: {} }
    cache.set(filename, module)
    const code = ts.transpileModule(fs.readFileSync(filename, 'utf8'), {
      compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX }, fileName: filename,
    }).outputText
    const requireLocal = id => {
      if (Object.hasOwn(mocks, id)) return mocks[id]
      if (!id.startsWith('.')) throw new Error(`Unmocked dependency: ${id}`)
      let resolved = path.resolve(path.dirname(filename), id)
      if (!path.extname(resolved)) resolved += fs.existsSync(resolved + '.ts') ? '.ts' : '.tsx'
      return load(resolved)
    }
    const context = { console, URL, URLSearchParams, Response, Request, Headers, Buffer, Uint8Array, atob,
      setTimeout, clearTimeout, setInterval, clearInterval, ...globals }
    vm.runInNewContext(`(function(require,module,exports){${code}\n})`, context, { filename })(requireLocal, module, module.exports)
    return module.exports
  }
  return load(path.join(root, entry))
}
