const gulp = require("gulp");
const ts = require("gulp-typescript");
const tsProject = ts.createProject("tsconfig.json");
const del = require('del');

let paths = {
  ts: ['./src/**/*.ts'],
  config: ['./src/config/**/*']
};

gulp.task("clean", function() {
  return del(["build/**"]);
});

gulp.task("compile", ['clean'], function () {
    return tsProject.src()
        .pipe(tsProject())
        .js.pipe(gulp.dest("build"));
});

gulp.task('watch', () => {
  gulp.watch(paths.ts, ['compile']);
  gulp.watch(paths.config, ['config']);
});

gulp.task("config", ['clean'], function() {
    return gulp.src("./src/config/**/*")
        .pipe(gulp.dest("build/config"));
});

gulp.task("default", ["compile", "config"]);
