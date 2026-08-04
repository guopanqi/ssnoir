namespace Schemy
{
    /// <summary>
    /// The core macro prelude, evaluated by every <see cref="Interpreter"/> before any
    /// host code runs. Without it there is no <c>let</c>, <c>cond</c>, <c>and</c>,
    /// <c>or</c>, <c>when</c> or <c>unless</c> — the language is not itself.
    ///
    /// This lives as a compiled-in string on purpose. It used to be an
    /// <c>&lt;EmbeddedResource&gt;</c> read back via
    /// <c>Assembly.GetManifestResourceStream</c>, which works everywhere the CLR runs
    /// the assembly as-is, but not under Unity's IL2CPP: the managed assembly is
    /// converted to C++ and the resource does not survive the trip. The interpreter
    /// then came up silently without a prelude, and the first <c>(let () ...)</c> in
    /// the host's own scripts failed as a bare empty list. A string constant is code,
    /// so it survives whatever a build does to assemblies — which is what
    /// "独立、Unity 友好" has to mean in practice.
    ///
    /// Order matters: later macros may depend on earlier ones. Edit this as Scheme;
    /// it is a verbatim string, so the only character needing care is the double
    /// quote (doubled to escape) — the prelude currently uses none.
    /// </summary>
    internal static class Init
    {
        public const string Source = @"
;; Core macros loaded automatically by every Interpreter instance.
;; Order matters: later macros may depend on earlier ones.

;; ── Step 1: simple let (bootstrap — needed before letrec/let* exist) ──────────
(define-macro let
  (lambda args
    (define specs (car args))
    (define bodies (cdr args))
    (if (null? specs)
      ;; Empty bindings still introduce a fresh scope: internal defines must stay
      ;; local, not leak to the enclosing/global env. Wrap in an immediately
      ;; invoked (lambda () ...), not (begin ...).
      `((lambda () ,@bodies))
      (begin
        (define spec1 (car specs))
        (define spec_rest (cdr specs))
        `(let ,spec_rest
           ((lambda ,(list (car spec1)) ,@bodies)
            ,(car (cdr spec1))))))))

;; ── Step 2: cond ─────────────────────────────────────────────────────────────
(define-macro cond
  (lambda args
    (if (= 0 (length args))
      '(if #f #f)
      (begin
        (define first (car args))
        (define rest (cdr args))
        (define test (if (equal? (car first) 'else) '#t (car first)))
        (define exprs (cdr first))
        `(if ,test
           (begin ,@exprs)
           (cond ,@rest))))))

;; ── Step 3: letrec — uses lambda + set!, no higher macros needed ──────────────
(define-macro letrec
  (lambda args
    (define specs (car args))
    (define bodies (cdr args))
    (define vars  (map car specs))
    (define sets  (map (lambda (s) (list 'set! (car s) (car (cdr s)))) specs))
    (define undefs (map (lambda (v) #f) vars))
    `((lambda ,vars ,@sets ,@bodies) ,@undefs)))

;; ── Step 4: let* — sequential bindings ───────────────────────────────────────
(define-macro let*
  (lambda args
    (define specs (car args))
    (define bodies (cdr args))
    (if (null? specs)
      ;; Empty bindings still introduce a fresh scope (see `let` above).
      `((lambda () ,@bodies))
      `(let (,(car specs))
         (let* ,(cdr specs) ,@bodies)))))

;; ── Step 5: redefine let to support named let ─────────────────────────────────
;;   (let loop ((i 0)) body)  →  (letrec ((loop (lambda (i) body))) (loop 0))
(define-macro let
  (lambda args
    (if (symbol? (car args))
      (begin
        (define name  (car args))
        (define specs (car (cdr args)))
        (define bodies (cdr (cdr args)))
        (define vars  (map car specs))
        (define inits (map (lambda (s) (car (cdr s))) specs))
        `(letrec ((,name (lambda ,vars ,@bodies)))
           (,name ,@inits)))
      (begin
        (define specs (car args))
        (define bodies (cdr args))
        (if (null? specs)
          ;; Empty bindings still introduce a fresh scope (see Step 1).
          `((lambda () ,@bodies))
          (begin
            (define spec1 (car specs))
            (define spec_rest (cdr specs))
            `(let ,spec_rest
               ((lambda ,(list (car spec1)) ,@bodies)
                ,(car (cdr spec1))))))))))

;; ── Step 6: and / or ─────────────────────────────────────────────────────────
(define-macro and
  (lambda args
    (cond
      ((null? args) '#t)
      ((null? (cdr args)) (car args))
      (else `(if ,(car args) (and ,@(cdr args)) #f)))))

(define-macro or
  (lambda args
    (cond
      ((null? args) '#f)
      ((null? (cdr args)) (car args))
      (else
        `(let ((__or__ ,(car args)))
           (if __or__ __or__ (or ,@(cdr args))))))))

;; ── Step 7: when / unless ────────────────────────────────────────────────────
(define-macro when
  (lambda args
    `(if ,(car args) (begin ,@(cdr args)) '())))

(define-macro unless
  (lambda args
    `(if ,(car args) '() (begin ,@(cdr args)))))

;; ── Step 8: common cXr combinations ─────────────────────────────────────────
(define (cadr x)   (car (cdr x)))
(define (cddr x)   (cdr (cdr x)))
(define (caar x)   (car (car x)))
(define (cdar x)   (cdr (car x)))
(define (caddr x)  (car (cdr (cdr x))))
(define (cdddr x)  (cdr (cdr (cdr x))))
(define (caadr x)  (car (car (cdr x))))
(define (cdadr x)  (cdr (car (cdr x))))
(define (cadar x)  (car (cdr (car x))))
(define (cddar x)  (cdr (cdr (car x))))
(define (cadddr x) (car (cdr (cdr (cdr x)))))
";
    }
}
