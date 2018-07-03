const gulp = require('gulp');
const gulpSequence = require('gulp-sequence');
const tslintPlugin = require('gulp-tslint');
const ts = require('gulp-typescript');
const tsProject = ts.createProject('tsconfig.json');
const del = require('del');

const paths = {
  build: './build/**',
  src: './src/**/*.ts',
  config: './src/config/**/*',
  static: ['./Dockerfile', './package.json', './package-lock.json']
};

gulp.task('lint', function() {
  return tsProject
    .src()
    .pipe(
      tslintPlugin({
        formatter: 'verbose'
      })
    )
    .pipe(tslintPlugin.report());
});

gulp.task('clean', () => {
  return del.sync(paths.build);
});

gulp.task('compile', () => {
  return tsProject
    .src()
    .pipe(tsProject())
    .js.pipe(gulp.dest('build'));
});

gulp.task('static', () => {
  return gulp.src(paths.static).pipe(gulp.dest('build'));
});

gulp.task('config', () => {
  return gulp.src(paths.config).pipe(gulp.dest('build/config'));
});

gulp.task('watch', () => {
  gulp.watch(paths.src, ['compile', 'lint']);
  gulp.watch(paths.static, ['static']);
  gulp.watch(paths.config, ['config']);
});

gulp.task(
  'default',
  gulpSequence('clean', ['compile', 'static', 'config', 'lint'])
);
