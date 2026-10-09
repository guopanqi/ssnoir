declare module "biwascheme" {
  interface Interpreter {
    evaluate(source: string, callback?: (value: unknown) => void): unknown;
  }
  interface BiwaSchemeApi {
    Interpreter: new (onError?: (error: unknown) => void) => Interpreter;
  }
  const BiwaScheme: BiwaSchemeApi;
  export default BiwaScheme;
}
