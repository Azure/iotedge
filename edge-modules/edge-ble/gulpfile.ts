const gulp = require("gulp");
const tslintPlugin = require("gulp-tslint");
const ts = require("gulp-typescript");
const tsProject = ts.createProject("tsconfig.json");
const del = require("del");

const paths = {
    build:  "./build/**",
    config: "./src/config/**/*",
    src: "./src/**/*.ts"
};

gulp.task("lint", function() {
    return tsProject.src()
        .pipe(tslintPlugin({ configuration: "./tslint.json",
                                formatter: "verbose"}))
        .pipe(tslintPlugin.report());
});

gulp.task("clean", () => {
    return del.sync(paths.build);
});

const onCompile = () => {
    return tsProject.src()
        .pipe(tsProject())
        .js.pipe(gulp.dest("build"));
};

gulp.task("compile", ["clean", "lint"], onCompile);
gulp.task("compileWatch", ["lint"], onCompile);

const onConfig = () => {
    return gulp.src(paths.config)
        .pipe(gulp.dest("build/config"));
};

gulp.task("config", ["clean"], onConfig);
gulp.task("configWatch", onConfig);

gulp.task("watch", () => {
    gulp.watch(paths.src, ["compileWatch"]);
    gulp.watch(paths.config, ["configWatch"]);
});


gulp.task("default", ["compile", "config"]);
